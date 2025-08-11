using HutongGames.PlayMaker.Actions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Random = UnityEngine.Random;
using SFCore.Utils;
using GlobalEnums;
using Vasi;
using Modding;
using PaleCourtCharms;
using System.Linq;
namespace PaleCourtCharms
{
    public class AbyssalBloomBehaviour : MonoBehaviour
    {
        private HeroController _hc => HeroController.instance;
        private PlayerData _pd => PlayerData.instance;
        private tk2dSpriteAnimator _hcAnim;
        private GameObject _knightBall;
        private tk2dSpriteAnimator _knightBallAnim;
        private List<NailSlash> _nailSlashes;
        private ModifyBloomProps _modifyProps;

        private Coroutine _sideSlashCoro;
        private GameObject _sideSlash;
        private Coroutine _vertSlashCoro;
        private GameObject _shadeSlashContainer;
        private Coroutine _wallSlashCoro;
        private GameObject _wallSlash;

        //Transcandence. Crystalmaster
        private bool _crystalChecked = false;
        private bool _crystalAvailable = false;
        private Type _crystalType = null;
        private object _crystalInstance = null;
        private MethodInfo _crystalEquippedMethod = null;
        private const float BloomSpeedBonusL1 = 0.05f; 
        private const float BloomSpeedBonusL2 = 0.15f; 
        private bool _crystalEquippedCached = false;
        private int _level;
        private bool _fury;
        private int _shadeSlashNum = 1;
        private bool playingAudio;
        private float audioCooldown = 0.2f;
        private float damageScale => _pd.equippedCharm_16 ? 0.075f : 0.0625f;
        private float damageBuff => damageScale * (_pd.equippedCharm_27 ? Math.Max(_pd.joniHealthBlue - _pd.healthBlue, 0) :
            (_pd.maxHealth - _pd.health)) // Extra per missing mask
            * (_level == 2 ? 1.5f : 1f); // Multiplies current buff by 1.5 when using tendrils

        private void OnEnable()
        {
            _hcAnim = _hc.GetComponent<tk2dSpriteAnimator>();

            _knightBall = Instantiate(PaleCourtCharms.preloadedGO["Knight Ball"], _hc.transform);
            Vector3 localScale = _knightBall.transform.localScale;
            localScale.x *= -1;
            _knightBall.transform.localScale = localScale;
            _knightBall.transform.localPosition += new Vector3(-4.75f, -0.25f);
            _knightBallAnim = _knightBall.GetComponent<tk2dSpriteAnimator>();

            _nailSlashes = new List<NailSlash>
            {
                HeroController.instance.normalSlash,
                HeroController.instance.alternateSlash,
                HeroController.instance.downSlash,
                HeroController.instance.upSlash,
            };

            _modifyProps = GetComponent<ModifyBloomProps>();

            PlayMakerFSM _radControl = Instantiate(PaleCourtCharms.preloadedGO["Radiance"].LocateMyFSM("Control"), _hc.transform);
            PaleCourtCharms.Clips["Shade Slash"] = (AudioClip)_radControl.GetAction<AudioPlayerOneShotSingle>("Antic", 1).audioClip.Value;


            On.HealthManager.Hit += HealthManagerHit;
            On.HeroController.CancelAttack += HeroControllerCancelAttack;
            On.HeroController.CancelDownAttack += HeroControllerCancelDownAttack;
            On.HeroController.Attack += DoVoidAttack;
            On.tk2dSpriteAnimator.Play_string += Tk2dSpriteAnimatorPlay;
            On.KnightHatchling.OnEnable += KnightHatchlingOnEnable;
            On.SpriteFlash.FlashingFury += SpriteFlashFlashingFury;
            ModHooks.CharmUpdateHook += OnCharmUpdate;
            TryInitCrystalIntegration();
            UpdateCrystalEquippedCache(); 
            On.HeroController.Move += HeroControllerMoveHook;
        }

        private void OnDisable()
        {
            SetLevel(0);
            On.HealthManager.Hit -= HealthManagerHit;
            On.HeroController.CancelAttack -= HeroControllerCancelAttack;
            On.HeroController.CancelDownAttack -= HeroControllerCancelDownAttack;
            On.HeroController.Attack -= DoVoidAttack;
            On.tk2dSpriteAnimator.Play_string -= Tk2dSpriteAnimatorPlay;
            On.KnightHatchling.OnEnable -= KnightHatchlingOnEnable;
            On.SpriteFlash.FlashingFury -= SpriteFlashFlashingFury;
            On.HeroController.Move -= HeroControllerMoveHook;
            ModHooks.CharmUpdateHook -= OnCharmUpdate;
        }

        public void SetLevel(int level)
        {
            _level = level;
            switch (_level)
            {
                case 0:
                    ModifySlashColors(false);
                    _modifyProps.ResetProps();
                    break;
                case 1:
                    ModifySlashColors(true);
                    _modifyProps.ModifyPropsL1();
                    break;
                case 2:
                    ModifySlashColors(true);
                    _modifyProps.ModifyPropsL2();
                    break;
            }
        }

        public void SetFury(bool fury) => _fury = fury;
        private void OnCharmUpdate(PlayerData pd, HeroController hc) => UpdateCrystalEquippedCache();

        private void TryInitCrystalIntegration()
        {
            if (_crystalChecked) return;
            _crystalChecked = true;

            try
            {
                var modObj = ModHooks.GetMod("Transcendence");
                Assembly asm = null;
                if (modObj is Mod modInstance) asm = modInstance.GetType().Assembly;
                else
                    asm = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => { try { return a.GetType("Transcendence.Crystalmaster") != null; } catch { return false; } });

                if (asm == null) { _crystalAvailable = false; return; }

                _crystalType = asm.GetType("Transcendence.Crystalmaster");
                if (_crystalType == null) { _crystalAvailable = false; return; }

                var instanceProp = _crystalType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProp != null) _crystalInstance = instanceProp.GetValue(null);
                else
                {
                    var instanceField = _crystalType.GetField("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                    if (instanceField != null) _crystalInstance = instanceField.GetValue(null);
                }

                _crystalEquippedMethod = _crystalType.GetMethod("Equipped", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
                _crystalAvailable = (_crystalEquippedMethod != null);
            }
            catch
            {
                _crystalAvailable = false;
                _crystalInstance = null;
                _crystalEquippedMethod = null;
                _crystalType = null;
            }
        }

        private void UpdateCrystalEquippedCache()
        {
            if (!_crystalChecked) TryInitCrystalIntegration();

            if (!_crystalAvailable || _crystalEquippedMethod == null || _crystalType == null)
            {
                _crystalEquippedCached = false;
                return;
            }

            if (_crystalInstance == null && _crystalType != null)
            {
                try
                {
                    var instanceProp = _crystalType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (instanceProp != null) _crystalInstance = instanceProp.GetValue(null);
                    else
                    {
                        var instanceField = _crystalType.GetField("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                        if (instanceField != null) _crystalInstance = instanceField.GetValue(null);
                    }
                }
                catch { _crystalInstance = null; }
            }

            if (_crystalInstance == null)
            {
                _crystalEquippedCached = false;
                return;
            }

            try
            {
                _crystalEquippedCached = (bool)_crystalEquippedMethod.Invoke(_crystalInstance, null);
            }
            catch
            {
                _crystalEquippedCached = false;
            }
        }
        private void HeroControllerMoveHook(On.HeroController.orig_Move orig, HeroController self, float dir)
        {
            orig(self, dir);

            // only apply bloom multiplier when there's an active bloom level and crystalmaster is present
            if (_level <= 0) return;

            if (!_crystalEquippedCached) return;

            float bonus = (_level == 2) ? BloomSpeedBonusL2 : BloomSpeedBonusL1;

            try
            {
                if (self.TryGetComponent<Rigidbody2D>(out var rb))
                {
                    var v = rb.velocity;
                    if (Mathf.Abs(v.x) > 0.001f)
                    {
                        v.x *= (1f + bonus);
                        rb.velocity = v;
                    }
                }
            }
            catch
            {
                
            }
        }

        private void ModifySlashColors(bool modify)
        {
            foreach (NailSlash nailSlash in _nailSlashes)
            {
                nailSlash.SetFury(modify);
            }

            Color color = modify ? Color.black : Color.white;

            foreach (GameObject slash in new GameObject[]
            {
                _hc.slashPrefab,
                _hc.slashAltPrefab,
                _hc.downSlashPrefab,
                _hc.upSlashPrefab,
                _hc.wallSlashPrefab
            })
            {
                slash.GetComponent<tk2dSprite>().color = color;
            }

            GameObject attacks = HeroController.instance.gameObject.FindGameObjectInChildren("Attacks");

            foreach (string child in new[] { "Cyclone Slash", "Dash Slash", "Great Slash" })
            {
                attacks.FindGameObjectInChildren(child).GetComponent<tk2dSprite>().color = color;
                foreach (var item in attacks.FindGameObjectInChildren(child).GetComponentsInChildren<tk2dSprite>())
                    item.color = color;
            }
        }

        private void HealthManagerHit(On.HealthManager.orig_Hit orig, HealthManager self, HitInstance hitInstance)
        {
            if (hitInstance.AttackType is AttackTypes.Nail or AttackTypes.NailBeam)
            {
                hitInstance.Multiplier += damageBuff + (_fury ? 0.75f : 0f);
            }
            if (hitInstance.AttackType == AttackTypes.SharpShadow)
            {
                hitInstance.Multiplier += 2f * damageBuff;
            }
            //Log("Multiplier is currently " + damageBuff + " to deal total damage of " + hitInstance.DamageDealt * hitInstance.Multiplier);
            try
            {
                //empty try catch,used to have logging.
                //exists so the timing on applying lemm strength is consistat and not rng
            }
            catch (Exception)
            {

            }

            if (hitInstance.AttackType is AttackTypes.Nail or AttackTypes.NailBeam)
            {

                hitInstance.Multiplier += damageBuff + (_fury ? 0.75f : 0f);
            }
   
             if (hitInstance.AttackType == AttackTypes.SharpShadow)
            {
                hitInstance.Multiplier += 2f * damageBuff;
            }

            orig(self, hitInstance);
        }

        private void HeroControllerCancelDownAttack(On.HeroController.orig_CancelDownAttack orig, HeroController self)
        {
            if (_vertSlashCoro != null)
            {
                CancelVerticalTendrilAttack();
            }
            orig(self);
        }

        private void HeroControllerCancelAttack(On.HeroController.orig_CancelAttack orig, HeroController self)
        {
            if (_sideSlashCoro != null)
            {
                CancelTendrilAttack();
            }
            orig(self);
        }

        private void Tk2dSpriteAnimatorPlay(On.tk2dSpriteAnimator.orig_Play_string orig, tk2dSpriteAnimator self, string name)
        {
            if (self.gameObject == _hc.gameObject && name == "Idle Hurt")
            {
                self.Play("Idle");
                return;
            }
            orig(self, name);
        }

        private void SpriteFlashFlashingFury(On.SpriteFlash.orig_FlashingFury orig, SpriteFlash self)
        {
            self.flash(Color.black, 0.75f, 0.25f, 0.01f, 0.25f);
            Mirror.SetField(self, "repeatFlash", true);
        }

        private void KnightHatchlingOnEnable(On.KnightHatchling.orig_OnEnable orig, KnightHatchling self)
        {
            orig(self);
            if (_level == 2)
            {
                KnightHatchling.TypeDetails details = Mirror.GetField<KnightHatchling, KnightHatchling.TypeDetails>(self, "details");
                Mirror.SetField(self, "details", details with { damage = details.damage * 2 });
            }
        }

        private void DoVoidAttack(On.HeroController.orig_Attack origAttack, HeroController hc, AttackDirection dir)
        {
            if (_level != 2)
            {
                origAttack(hc, dir);
                return;
            }

            InputHandler ih = InputHandler.Instance;
            if (_pd.GetBool(nameof(PlayerData.equippedCharm_32)))
            {
                Mirror.SetField(_hc, "attackDuration", _hc.ATTACK_DURATION_CH);
                Mirror.SetField(_hc, "attack_cooldown", _hc.ATTACK_COOLDOWN_TIME_CH);
            }
            else
            {
                Mirror.SetField(_hc, "attackDuration", _hc.ATTACK_DURATION);
                Mirror.SetField(_hc, "attack_cooldown", _hc.ATTACK_COOLDOWN_TIME);
            }
            _hc.cState.recoiling = false;

            if (hc.cState.wallSliding)
            {
                if (_hc.cState.attacking) CancelWallTendrilAttack();
                _wallSlashCoro = StartCoroutine(WallTendrilAttack());
            }
            else if (ih.ActionButtonToPlayerAction(HeroActionButton.DOWN) && !hc.CheckTouchingGround())
            {
                if (_hc.cState.attacking) CancelVerticalTendrilAttack();
                _vertSlashCoro = StartCoroutine(VerticalTendrilAttack(false));
            }
            else if (ih.ActionButtonToPlayerAction(HeroActionButton.UP))
            {
                if (_hc.cState.attacking) CancelVerticalTendrilAttack();
                _vertSlashCoro = StartCoroutine(VerticalTendrilAttack(true));
            }
            else
            {
                if (_hc.cState.attacking)
                {
                    CancelTendrilAttack();
                    _shadeSlashNum = _shadeSlashNum == 1 ? 2 : 1;
                }
                _sideSlashCoro = StartCoroutine(TendrilAttack());
            }
        }

        public void CancelTendrilAttack()
        {

            if (_sideSlashCoro != null) StopCoroutine(_sideSlashCoro);
            Destroy(_sideSlash);
            _knightBall.SetActive(false);
            _hc.GetComponent<MeshRenderer>().enabled = true;
            ResetTendrilAttack();
        }

        public void CancelVerticalTendrilAttack()
        {

            if (_vertSlashCoro != null) StopCoroutine(_vertSlashCoro);
            Destroy(_shadeSlashContainer);
            _hc.StartAnimationControl();
            ResetTendrilAttack();
        }

        public void CancelWallTendrilAttack()
        {

            if (_wallSlashCoro != null) StopCoroutine(_wallSlashCoro);
            Destroy(_wallSlash);
            _hc.StartAnimationControl();
            ResetTendrilAttack();
        }

        private void ResetTendrilAttack()
        {
            _hc.cState.attacking = false;
            _hc.cState.upAttacking = false;
            _hc.cState.downAttacking = false;
            Mirror.SetField(_hc, "attack_time", 0f);
        }

        private IEnumerator TendrilAttack()
        {
            _hc.cState.attacking = true;

            MeshRenderer mr = _hc.GetComponent<MeshRenderer>();
            if (!playingAudio) StartCoroutine(PlayAudio());

            mr.enabled = false;
            _knightBall.SetActive(true);


            Destroy(_sideSlash);

            _sideSlash = new GameObject("Shade Slash");
            _sideSlash.transform.parent = _knightBall.transform;
            _sideSlash.layer = (int)PhysLayers.HERO_ATTACK;
            _sideSlash.tag = "Nail Attack";
            _sideSlash.transform.localPosition = Vector3.zero;
            _sideSlash.transform.localScale = Vector3.one;
            _sideSlash.SetActive(false);

            AddDamageEnemiesFsm(_sideSlash, AttackDirection.normal);


            PolygonCollider2D slashPoly = _sideSlash.AddComponent<PolygonCollider2D>();
            slashPoly.points = new[]
            {
        new Vector2(0.0f, -2.0f),
        new Vector2(3.5f, -2.0f),
        new Vector2(3.5f, 0.0f),
        new Vector2(3.0f, 1.0f),
        new Vector2(0.0f, 2.0f),
        new Vector2(-3f, 0.0f) // covers player body

            };

            slashPoly.offset = Vector2.zero;
            slashPoly.isTrigger = true;


            var kb = _sideSlash.AddComponent<ShadeSlashKnockback>();
            kb.heroCtrl = _hc;


            GameObject parrySlash = Instantiate(_sideSlash, _sideSlash.transform);
            parrySlash.LocateMyFSM("damages_enemy").GetFsmIntVariable("damageDealt").Value = 0;
            parrySlash.layer = (int)PhysLayers.ITEM;


            ShadeSlash ss = _sideSlash.AddComponent<ShadeSlash>();
            ss.attackDirection = AttackDirection.normal;


            _sideSlash.SetActive(true);
            parrySlash.SetActive(true);


            _knightBallAnim.PlayFromFrame("Slash" + _shadeSlashNum + " Antic", 2);
            yield return new WaitWhile(() => _knightBallAnim.IsPlaying("Slash" + _shadeSlashNum + " Antic"));
            yield return new WaitForSeconds(_knightBallAnim.PlayAnimGetTime("Slash" + _shadeSlashNum) - (1f / 24f));


            Destroy(_sideSlash);
            mr.enabled = true;
            _knightBall.SetActive(false);
            _shadeSlashNum = _shadeSlashNum == 1 ? 2 : 1;
            _hc.cState.attacking = false;
        }


        private IEnumerator VerticalTendrilAttack(bool up)
        {
            _hc.cState.attacking = true;
            if (up) _hc.cState.upAttacking = true;
            else _hc.cState.downAttacking = true;

            string animName = up ? "Up" : "Down";

            _hc.StopAnimationControl();
            if (!playingAudio) StartCoroutine(PlayAudio());

            _hcAnim.Play(animName + "Slash Void");
            tk2dSpriteAnimationClip hcSlashAnim = _hcAnim.GetClipByName(animName + "Slash Void");
            _hcAnim.Play(hcSlashAnim);

            // Create slash objects
            Destroy(_shadeSlashContainer);
            _shadeSlashContainer = Instantiate(new GameObject("Shade Slash Container"), _hc.transform);
            _shadeSlashContainer.layer = (int)PhysLayers.HERO_ATTACK;
            _shadeSlashContainer.SetActive(false);

            GameObject shadeSlash = new GameObject("Shade Slash");
            shadeSlash.transform.parent = _shadeSlashContainer.transform;
            shadeSlash.layer = (int)PhysLayers.HERO_ATTACK;
            shadeSlash.tag = "Nail Attack";
            shadeSlash.transform.localPosition = new Vector3(0f, up ? 1.0f : -2.0f, 0f);
            shadeSlash.transform.localScale = new Vector3(2f, 2f, 2f);

            AddDamageEnemiesFsm(shadeSlash, up ? AttackDirection.upward : AttackDirection.downward);

            // Create hitboxes
            PolygonCollider2D slashPoly = shadeSlash.AddComponent<PolygonCollider2D>();
            if (up) slashPoly.points = new[]
            {
                new Vector2(-1f, 0f),
                new Vector2(-0.75f, 1.5f),
                new Vector2(-0.5f, 2.0f),
                new Vector2(0f, 2.25f),
                new Vector2(0.5f, 2.0f),
                new Vector2(0.75f, 1.5f),
                new Vector2(1f, 0f),
                new Vector2(0f, 0.5f)
            };
            else slashPoly.points = new[]
            {
                new Vector2(-1f, -0f),
                new Vector2(-1.25f, -0.5f),
                new Vector2(-0.875f, -1.5f),
                new Vector2(-0.5f, -1.9f),
                new Vector2(0f, -2.2f),
                new Vector2(0.5f, -2.0f),
                new Vector2(0.875f, -1.5f),
                new Vector2(1.25f, -0.5f),
                new Vector2(1f, -0f),
                new Vector2(0f, 0.5f)
            };
            slashPoly.offset = new Vector2(0.0f, up ? -1f : 0.75f);
            slashPoly.isTrigger = true;

            GameObject parrySlash = Instantiate(shadeSlash, shadeSlash.transform);
            parrySlash.LocateMyFSM("damages_enemy").GetFsmIntVariable("damageDealt").Value = 0;
            parrySlash.layer = (int)PhysLayers.ITEM;
            parrySlash.transform.localPosition = Vector3.zero;
            parrySlash.transform.localScale = Vector3.one;

            shadeSlash.AddComponent<MeshRenderer>();
            shadeSlash.AddComponent<MeshFilter>();
            tk2dSprite slashSprite = shadeSlash.AddComponent<tk2dSprite>();
            tk2dSpriteAnimator slashAnim = shadeSlash.AddComponent<tk2dSpriteAnimator>();
            slashSprite.Collection = _hc.GetComponent<tk2dSprite>().Collection;
            slashAnim.Library = _hcAnim.Library;

            ShadeSlash ss = shadeSlash.AddComponent<ShadeSlash>();
            ss.attackDirection = up ? AttackDirection.upward : AttackDirection.downward;

            _shadeSlashContainer.SetActive(true);

            yield return new WaitForSeconds(slashAnim.PlayAnimGetTime(animName + "Slash Effect"));

            Destroy(_shadeSlashContainer);

            yield return new WaitWhile(() => _hcAnim.Playing && _hcAnim.IsPlaying(animName + "Slash Void"));
            _hc.StartAnimationControl();
            _hc.cState.attacking = false;
            if (up) _hc.cState.upAttacking = false;
            else _hc.cState.downAttacking = false;
        }

        private IEnumerator WallTendrilAttack()
        {
            _hc.cState.attacking = true;

            _hc.StopAnimationControl();

            if (!playingAudio) StartCoroutine(PlayAudio());

            _hcAnim.Play(_hcAnim.GetClipByName("WallSlash Void"));

            Destroy(_wallSlash);
            _wallSlash = new GameObject("Shade Slash");
            _wallSlash.transform.parent = _hc.transform;
            _wallSlash.layer = (int)PhysLayers.HERO_ATTACK;
            _wallSlash.tag = "Nail Attack";
            _wallSlash.transform.localPosition = new Vector3(0f, 1f, 0f);
            _wallSlash.transform.localScale = Vector3.one;
            _wallSlash.SetActive(false);

            AddDamageEnemiesFsm(_wallSlash, AttackDirection.normal);

            PolygonCollider2D slashPoly = _wallSlash.AddComponent<PolygonCollider2D>();
            slashPoly.points = new[]
            {
                new Vector2(-1.5f, 2.0f),
                new Vector2(1.0f, 1.5f),
                new Vector2(3.0f, -1.0f),
                new Vector2(1.0f, -2.0f),
                new Vector2(-1.5f, -1.0f),
            };
            slashPoly.offset = new Vector2(1f, 0f);
            slashPoly.isTrigger = true;

            GameObject parrySlash = Instantiate(_wallSlash, _wallSlash.transform);
            parrySlash.LocateMyFSM("damages_enemy").GetFsmIntVariable("damageDealt").Value = 0;
            parrySlash.layer = (int)PhysLayers.ITEM;
            parrySlash.transform.localPosition = Vector3.zero;
            parrySlash.transform.localScale = Vector3.one;
            parrySlash.SetActive(false);

            _wallSlash.AddComponent<MeshRenderer>();
            _wallSlash.AddComponent<MeshFilter>();
            tk2dSprite slashSprite = _wallSlash.AddComponent<tk2dSprite>();
            tk2dSpriteAnimator slashAnim = _wallSlash.AddComponent<tk2dSpriteAnimator>();
            slashSprite.Collection = _hc.GetComponent<tk2dSprite>().Collection;
            slashAnim.Library = _hcAnim.Library;

            ShadeSlash ss = _wallSlash.AddComponent<ShadeSlash>();
            ss.attackDirection = AttackDirection.normal;

            _wallSlash.SetActive(true);
            parrySlash.SetActive(true);

            yield return new WaitForSeconds(slashAnim.PlayAnimGetTime("Slash Effect"));

            Destroy(_wallSlash);

            _hc.StartAnimationControl();
            _hc.cState.attacking = false;
        }
        private IEnumerator PlayAudio()
        {
            playingAudio = true;
            this.PlayAudio(PaleCourtCharms.Clips["Shade Slash"], 0.7f);
            yield return new WaitForSeconds(audioCooldown);
            playingAudio = false;
        }

        private void AddDamageEnemiesFsm(GameObject o, AttackDirection dir)
        {
            PlayMakerFSM tempFsm = o.AddComponent<PlayMakerFSM>();
            PlayMakerFSM fsm = _hc.gameObject.Find("AltSlash").LocateMyFSM("damages_enemy");
            foreach (var fi in typeof(PlayMakerFSM).GetFields(BindingFlags.Instance | BindingFlags.NonPublic |
                                                              BindingFlags.Public))
            {
                fi.SetValue(tempFsm, fi.GetValue(fsm));
            }
            switch (dir)
            {
                case AttackDirection.normal:
                    tempFsm.GetFsmFloatVariable("direction").Value = _hc.cState.facingRight ? 0f : 180f;
                    break;
                case AttackDirection.upward:
                    tempFsm.GetFsmFloatVariable("direction").Value = 90f;
                    break;
                case AttackDirection.downward:
                    tempFsm.GetFsmFloatVariable("direction").Value = 270f;
                    break;
            }
        }


    }
[RequireComponent(typeof(PolygonCollider2D))]
public class ShadeSlashKnockback : MonoBehaviour
{
    public HeroController heroCtrl;
    private bool _hasBounced;

   
    private const int WALL_LAYER = 8;
    private const float MIN_PCT = 0.6f;     

    private const float TIP_RIGHT =  3.5f;
    private const float TIP_LEFT  = -3.0f;

    private PolygonCollider2D _poly;

    void Awake()
    {
        _poly = GetComponent<PolygonCollider2D>();
        _poly.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_hasBounced || other.gameObject.layer != WALL_LAYER)
            return;

        // horizontal‐vertical guard to not bounce of floors or ceilings
        Vector2 center   = transform.position;
        Vector2 hitPoint = other.ClosestPoint(center);
        Vector2 dc       = hitPoint - center;
        if (Mathf.Abs(dc.x) <= Mathf.Abs(dc.y))
            return;

        bool goingRight = dc.x > 0f;
        float fullReach = goingRight ? TIP_RIGHT : -TIP_LEFT;
        Vector2 dir     = new Vector2(goingRight ? 1f : -1f, 0f);

     
        RaycastHit2D hit = Physics2D.Raycast(center, dir, fullReach + 0.001f, 1 << WALL_LAYER);
        if (!hit.collider)
            return;  

     
        float pen = fullReach - hit.distance;
        if (pen <= 0f)
            return;

        if (pen < fullReach * MIN_PCT)
            return;

        if (goingRight) heroCtrl.RecoilLeft();
        else            heroCtrl.RecoilRight();

        if (heroCtrl.TryGetComponent<Rigidbody2D>(out var rb))
            rb.velocity *= 0.5f;

        _hasBounced = true;
        Destroy(this);
    }
}

}

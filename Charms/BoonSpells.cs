using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using SFCore.Utils;
using Random = UnityEngine.Random;
using System.Collections;
using HutongGames.PlayMaker.Actions;
using Modding;
using System.Linq;

namespace PaleCourtCharms
{
    public class BoonSpells : MonoBehaviour
    {
        private HeroController _hc = HeroController.instance;
        private PlayMakerFSM _spellControl;
        private GameObject _audioPlayer;

        private const float DaggerSpeed = 50f;
        private const int BlastDamage = 35;
        private const int BlastDamageShaman = 45;

        // Transcendence. Shaman Amp integration
        private bool _transcChecked = false;
        private bool _transcAvailable = false;
        private object _shamanInstance = null;
        private MethodInfo _shamanEquippedMethod = null;
        private MethodInfo _shamanEnlargeStatic = null;
        private Type _shamanType = null;
        private bool _shamanEquippedCached = false;

        // Transcendence. Snail Soul integration
        private bool _snailChecked = false;
        private bool _snailAvailable = false;
        private object _snailInstance = null;
        private MethodInfo _snailEquippedMethod = null;
        private Type _snailType = null;
        private bool _snailEquippedCached = false;
        private const float SnailSlowdown = 4f;

        // Let Vespa's Vengeance take priority
        private bool _vespaChecked = false;
        private bool _vespaAvailable = false;
        private object _vespaInstance = null;
        private MethodInfo _vespaEquippedMethod = null;
        private Type _vespaType = null;
        private bool _vespaEquippedCached = false;

        private void OnEnable()
        {
            if (HeroController.instance == null || HeroController.instance.spellControl == null)
                return;

            InitOnce();
        }

        private void InitOnce()
        {
            try
            {
                _hc = HeroController.instance;
                _spellControl = _hc?.spellControl;
                if (_spellControl == null) return;

                GameObject fireballParent = _spellControl.GetAction<SpawnObjectFromGlobalPool>("Fireball 2", 3).gameObject.Value;
                PlayMakerFSM fireballCast = fireballParent.LocateMyFSM("Fireball Cast");
                _audioPlayer = fireballCast.GetAction<AudioPlayerOneShotSingle>("Cast Right", 3).audioPlayer.Value;

                PlayMakerFSM _pvControl = Instantiate(PaleCourtCharms.preloadedGO["PV"].LocateMyFSM("Control"), _hc.transform);

                if (!PaleCourtCharms.preloadedGO.ContainsKey("Plume"))
                {
                    GameObject plume = Instantiate(_pvControl.GetAction<SpawnObjectFromGlobalPool>("Plume Gen", 0).gameObject.Value);
                    plume.SetActive(false);
                    plume.layer = (int)GlobalEnums.PhysLayers.HERO_ATTACK;
                    plume.tag = "Hero Spell";
                    Destroy(plume.GetComponent<DamageHero>());
                    DontDestroyOnLoad(plume);
                    PaleCourtCharms.preloadedGO["Plume"] = plume;
                }

                if (!PaleCourtCharms.preloadedGO.ContainsKey("BoonDagger"))
                {
                    GameObject dagger = Instantiate(_pvControl.GetAction<FlingObjectsFromGlobalPoolTime>("SmallShot LowHigh").gameObject.Value);
                    dagger.SetActive(false);
                    dagger.layer = (int)GlobalEnums.PhysLayers.HERO_ATTACK;
                    dagger.tag = "Hero Spell";
                    Destroy(dagger.GetComponent<DamageHero>());
                    Destroy(dagger.LocateMyFSM("Control"));
                    var dribble = dagger.FindGameObjectInChildren("Dribble L");
                    if (dribble != null) dribble.layer = 9;
                    var glow = dagger.FindGameObjectInChildren("Glow");
                    if (glow != null) glow.layer = 9;
                    var beam = dagger.FindGameObjectInChildren("Beam");
                    if (beam != null) beam.layer = 9;
                    DontDestroyOnLoad(dagger);
                    PaleCourtCharms.preloadedGO["BoonDagger"] = dagger;
                }

                PaleCourtCharms.Clips["Burst"] = (AudioClip)_pvControl.GetAction<AudioPlayerOneShotSingle>("Focus Burst", 8).audioClip.Value;
                PaleCourtCharms.Clips["Plume Up"] = (AudioClip)_pvControl.GetAction<AudioPlayerOneShotSingle>("Plume Up", 1).audioClip.Value;

                ModifySpellFSM(true);

                ModHooks.CharmUpdateHook += OnCharmUpdate;
                UpdateAllCharmCaches();
            }
            catch (Exception)
            {
                // swallow to avoid breaking initialization
            }
        }

        private void OnDisable()
        {
            ModifySpellFSM(false);
            ModHooks.CharmUpdateHook -= OnCharmUpdate;
        }

        private void OnCharmUpdate(PlayerData pd, HeroController hc)
        {
            UpdateAllCharmCaches();
        }

        private void UpdateAllCharmCaches()
        {
            UpdateShamanAmpEquippedCache();
            UpdateSnailEquippedCache();
            UpdateVespaEquippedCache();
        }

        private void UpdateShamanAmpEquippedCache()
        {
            if (!_transcChecked) TryInitTranscendenceIntegration();

            if (!_transcAvailable || _shamanEquippedMethod == null || _shamanType == null)
            {
                _shamanEquippedCached = false;
                return;
            }

            if (_shamanInstance == null && _shamanType != null)
            {
                try
                {
                    var instanceProp = _shamanType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (instanceProp != null) _shamanInstance = instanceProp.GetValue(null);
                    else
                    {
                        var instanceField = _shamanType.GetField("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                        if (instanceField != null) _shamanInstance = instanceField.GetValue(null);
                    }
                }
                catch { _shamanInstance = null; }
            }

            if (_shamanInstance == null)
            {
                _shamanEquippedCached = false;
                return;
            }

            try
            {
                _shamanEquippedCached = (bool)_shamanEquippedMethod.Invoke(_shamanInstance, null);
            }
            catch { _shamanEquippedCached = false; }
        }

        private void UpdateSnailEquippedCache()
        {
            if (!_snailChecked) TryInitSnailIntegration();

            if (!_snailAvailable || _snailEquippedMethod == null || _snailType == null)
            {
                _snailEquippedCached = false;
                return;
            }

            if (_snailInstance == null && _snailType != null)
            {
                try
                {
                    var instanceProp = _snailType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (instanceProp != null) _snailInstance = instanceProp.GetValue(null);
                    else
                    {
                        var instanceField = _snailType.GetField("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                        if (instanceField != null) _snailInstance = instanceField.GetValue(null);
                    }
                }
                catch { _snailInstance = null; }
            }

            if (_snailInstance == null)
            {
                _snailEquippedCached = false;
                return;
            }

            try
            {
                _snailEquippedCached = (bool)_snailEquippedMethod.Invoke(_snailInstance, null);
            }
            catch { _snailEquippedCached = false; }
        }

        private void ModifySpellFSM(bool enabled)
        {
            if (_spellControl == null) return;

            if (enabled)
            {
                // If Vespa's vengence is equipped let it take priority
                if (!IsVespaEquipped())
                {
                    _spellControl.ChangeTransition("Level Check 3", "LEVEL 1", "Scream Antic1 Blasts");
                    _spellControl.ChangeTransition("Level Check 3", "LEVEL 2", "Scream Antic2 Blasts");
                }

                _spellControl.ChangeTransition("Quake1 Down", "HERO LANDED", "Q1 Land Plumes");
                _spellControl.ChangeTransition("Quake2 Down", "HERO LANDED", "Q2 Land Plumes");

                if (!PlayerData.instance.GetBool(nameof(PlayerData.equippedCharm_11)))
                {
                    _spellControl.ChangeTransition("Level Check", "LEVEL 1", "Fireball 1 SmallShots");
                    _spellControl.ChangeTransition("Level Check", "LEVEL 2", "Fireball 2 SmallShots");
                }
            }
            else
            {
                // When disabling only restore Scream antic transitions if Vespa is not equipped.
                if (!IsVespaEquipped())
                {
                    _spellControl.ChangeTransition("Level Check 3", "LEVEL 1", "Scream Antic1");
                    _spellControl.ChangeTransition("Level Check 3", "LEVEL 2", "Scream Antic2");
                }

                _spellControl.ChangeTransition("Quake1 Down", "HERO LANDED", "Quake1 Land");
                _spellControl.ChangeTransition("Quake2 Down", "HERO LANDED", "Q2 Land");

                _spellControl.ChangeTransition("Level Check", "LEVEL 1", "Fireball 1");
                _spellControl.ChangeTransition("Level Check", "LEVEL 2", "Fireball 2");
            }
        }

        // Transcendence (Shaman Amp) integration
        private void TryInitTranscendenceIntegration()
        {
            if (_transcChecked) return;
            _transcChecked = true;

            try
            {
                var modObj = ModHooks.GetMod("Transcendence");
                Assembly asm = null;
                if (modObj is Mod modInstance) asm = modInstance.GetType().Assembly;
                else
                {
                    asm = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a =>
                        {
                            try { return a.GetType("Transcendence.ShamanAmp") != null; }
                            catch { return false; }
                        });
                }

                if (asm == null) { _transcAvailable = false; return; }

                _shamanType = asm.GetType("Transcendence.ShamanAmp");
                if (_shamanType == null) { _transcAvailable = false; return; }

                var instanceProp = _shamanType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProp != null) _shamanInstance = instanceProp.GetValue(null);
                else
                {
                    var instanceField = _shamanType.GetField("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                    if (instanceField != null) _shamanInstance = instanceField.GetValue(null);
                }

                _shamanEquippedMethod = _shamanType.GetMethod("Equipped", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
                _shamanEnlargeStatic = _shamanType.GetMethod("Enlarge", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy, null, new Type[] { typeof(GameObject) }, null);

                _transcAvailable = (_shamanEquippedMethod != null) || (_shamanEnlargeStatic != null);
            }
            catch
            {
                _transcAvailable = false;
                _shamanInstance = null;
                _shamanEquippedMethod = null;
                _shamanEnlargeStatic = null;
                _shamanType = null;
            }
        }

        private void MaybeCallTranscendenceEnlarge(GameObject obj)
        {
            if (!_transcAvailable || _shamanEnlargeStatic == null) return;
            try { _shamanEnlargeStatic.Invoke(null, new object[] { obj }); }
            catch { }
        }

        // Snail Soul integration
        private void TryInitSnailIntegration()
        {
            if (_snailChecked) return;
            _snailChecked = true;

            try
            {
                var modObj = ModHooks.GetMod("Transcendence");
                Assembly asm = null;
                if (modObj is Mod modInstance) asm = modInstance.GetType().Assembly;
                else
                {
                    asm = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a =>
                        {
                            try { return a.GetType("Transcendence.SnailSoul") != null; }
                            catch { return false; }
                        });
                }

                if (asm == null) { _snailAvailable = false; return; }

                _snailType = asm.GetType("Transcendence.SnailSoul");
                if (_snailType == null) { _snailAvailable = false; return; }

                var instanceProp = _snailType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProp != null) _snailInstance = instanceProp.GetValue(null);
                else
                {
                    var instanceField = _snailType.GetField("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                    if (instanceField != null) _snailInstance = instanceField.GetValue(null);
                }

                _snailEquippedMethod = _snailType.GetMethod("Equipped", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
                _snailAvailable = (_snailEquippedMethod != null);
            }
            catch
            {
                _snailAvailable = false;
                _snailInstance = null;
                _snailEquippedMethod = null;
                _snailType = null;
            }
        }

        // Vespa integration init
        private void TryInitVespaIntegration()
        {
            if (_vespaChecked) return;
            _vespaChecked = true;
            try
            {
                var modObj = ModHooks.GetMod("Transcendence");
                Assembly asm = null;
                if (modObj is Mod modInstance) asm = modInstance.GetType().Assembly;
                else
                    asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => { try { return a.GetType("Transcendence.VespasVengeance") != null; } catch { return false; } });

                if (asm == null) { _vespaAvailable = false; return; }

                _vespaType = asm.GetType("Transcendence.VespasVengeance");
                if (_vespaType == null) { _vespaAvailable = false; return; }

                var instanceProp = _vespaType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProp != null) _vespaInstance = instanceProp.GetValue(null);
                else
                {
                    var instanceField = _vespaType.GetField("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                    if (instanceField != null) _vespaInstance = instanceField.GetValue(null);
                }

                _vespaEquippedMethod = _vespaType.GetMethod("Equipped", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
                _vespaAvailable = (_vespaEquippedMethod != null);
            }
            catch
            {
                _vespaAvailable = false;
                _vespaInstance = null;
                _vespaEquippedMethod = null;
                _vespaType = null;
            }
        }

        private void UpdateVespaEquippedCache()
        {
            if (!_vespaChecked) TryInitVespaIntegration();
            if (!_vespaAvailable || _vespaEquippedMethod == null || _vespaType == null) { _vespaEquippedCached = false; return; }

            if (_vespaInstance == null && _vespaType != null)
            {
                try
                {
                    var instanceProp = _vespaType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (instanceProp != null) _vespaInstance = instanceProp.GetValue(null);
                    else
                    {
                        var instanceField = _vespaType.GetField("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                        if (instanceField != null) _vespaInstance = instanceField.GetValue(null);
                    }
                }
                catch { _vespaInstance = null; }
            }

            if (_vespaInstance == null) { _vespaEquippedCached = false; return; }

            try { _vespaEquippedCached = (bool)_vespaEquippedMethod.Invoke(_vespaInstance, null); }
            catch { _vespaEquippedCached = false; }
        }

        private bool IsVespaEquipped()
        {
            if (!_vespaChecked) TryInitVespaIntegration();
            if (!_vespaAvailable || _vespaEquippedMethod == null || _vespaType == null) return false;

            if (_vespaInstance == null)
            {
                try
                {
                    var instanceProp = _vespaType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (instanceProp != null) _vespaInstance = instanceProp.GetValue(null);
                    else
                    {
                        var instanceField = _vespaType.GetField("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                        if (instanceField != null) _vespaInstance = instanceField.GetValue(null);
                    }
                }
                catch { _vespaInstance = null; }
            }

            if (_vespaInstance == null) return false;

            try
            {
                return (bool)_vespaEquippedMethod.Invoke(_vespaInstance, null);
            }
            catch
            {
                return false;
            }
        }

        public void CastDaggers(bool upgraded)
        {
            bool shaman = PlayerData.instance.equippedCharm_19;
            int angleMin = shaman ? -30 : -25;
            int angleMax = shaman ? 30 : 25;
            int increment = shaman ? 20 : 25;

            float snailMultiplier = 1f;
            if (_snailEquippedCached) snailMultiplier = 1f / SnailSlowdown;

            for (int angle = angleMin; angle <= angleMax; angle += increment)
            {
                GameObject dagger = Instantiate(PaleCourtCharms.preloadedGO["BoonDagger"],
                    HeroController.instance.transform.position, Quaternion.identity);
                dagger.SetActive(false);
                if (angle != angleMin) Destroy(dagger.GetComponent<AudioSource>());

                if (_shamanEquippedCached)
                {
                    MaybeCallTranscendenceEnlarge(dagger);
                }

                Rigidbody2D rb = dagger.GetComponent<Rigidbody2D>();
                rb.isKinematic = true;

                float baseXVel = DaggerSpeed * Mathf.Cos(Mathf.Deg2Rad * angle) * -HeroController.instance.transform.localScale.x;
                float baseYVel = DaggerSpeed * Mathf.Sin(Mathf.Deg2Rad * angle);

                float xVel = baseXVel * snailMultiplier;
                float yVel = baseYVel * snailMultiplier;

                dagger.SetActive(true);
                rb.velocity = new Vector2(xVel, yVel);

                dagger.AddComponent<Dagger>().upgraded = upgraded;

                Destroy(dagger, 5f);
            }
        }

        public void CastPlumes(bool upgraded)
        {
            for (float x = 2; x <= 10; x += 2)
            {
                Vector2 pos = HeroController.instance.transform.position;
                float plumeY = pos.y - 1.8f;

                GameObject plumeL = Instantiate(PaleCourtCharms.preloadedGO["Plume"], new Vector2(pos.x - x, plumeY), Quaternion.identity);

                if (_shamanEquippedCached)
                {
                    MaybeCallTranscendenceEnlarge(plumeL);
                }

                plumeL.SetActive(true);
                plumeL.AddComponent<Plume>().upgraded = upgraded;

                GameObject plumeR = Instantiate(PaleCourtCharms.preloadedGO["Plume"], new Vector2(pos.x + x, plumeY), Quaternion.identity);

                if (_shamanEquippedCached)
                {
                    MaybeCallTranscendenceEnlarge(plumeR);
                }

                plumeR.SetActive(true);
                plumeR.AddComponent<Plume>().upgraded = upgraded;
            }
            PlayAudio("Plume Up", 1.5f, 1.5f, 0.5f, 0.25f);
        }

        public void CastBlasts(bool upgraded)
        {
            List<GameObject> blasts = new List<GameObject>();

            IEnumerator CastBlastsCoro()
            {
                var first = SpawnBlast(HeroController.instance.transform.position + Vector3.up * 4f, upgraded);

                blasts.Add(first);
                yield return new WaitForSeconds(0.2f);

                for (int i = 0; i < (upgraded ? 3 : 1); i++)
                {
                    blasts.Add(SpawnBlast(HeroController.instance.transform.position +
                        Vector3.up * UnityEngine.Random.Range(4, 10) + Vector3.right * UnityEngine.Random.Range(-3, 3), upgraded));
                    yield return new WaitForSeconds(0.2f);
                }
            }
            IEnumerator DisableColliderCoro()
            {
                yield return new WaitForSeconds(0.15f);

                for (int i = 0; i < blasts.Count; i++)
                {
                    var b = blasts[i];
                    if (b != null) Destroy(b.GetComponent<CircleCollider2D>());
                    yield return new WaitForSeconds(0.2f);
                }
            }
            IEnumerator DestroyBlastsCoro()
            {
                yield return new WaitForSeconds(0.5f);

                for (int i = 0; i < blasts.Count; i++)
                {
                    var b = blasts[i];
                    if (b != null) Destroy(b);
                    yield return new WaitForSeconds(0.2f);
                }
            }
            StartCoroutine(CastBlastsCoro());
            StartCoroutine(DisableColliderCoro());
            StartCoroutine(DestroyBlastsCoro());
        }

        private GameObject SpawnBlast(Vector3 pos, bool upgraded)
        {
            GameObject blast = Instantiate(PaleCourtCharms.preloadedGO["Blast"], pos, Quaternion.identity);
            blast.layer = (int)GlobalEnums.PhysLayers.HERO_ATTACK;
            blast.tag = "Hero Spell";
            blast.SetActive(true);
            Destroy(blast.FindGameObjectInChildren("hero_damager"));

            Animator anim = blast.GetComponent<Animator>();
            int hash = anim.GetCurrentAnimatorStateInfo(0).fullPathHash;
            anim.PlayInFixedTime(hash, -1, 0.75f);

            if (_shamanEquippedCached)
            {
                MaybeCallTranscendenceEnlarge(blast);
            }

            CircleCollider2D col = blast.AddComponent<CircleCollider2D>();
            col.offset = Vector2.down;
            col.radius = 3.5f;
            col.isTrigger = true;

            float scale = Math.Max(Mathf.Abs(blast.transform.localScale.x), Mathf.Abs(blast.transform.localScale.y));
            if (!float.IsNaN(scale) && scale > 0f) col.radius *= scale;

            DamageEnemies de = blast.AddComponent<DamageEnemies>();
            de.damageDealt = PlayerData.instance.equippedCharm_19 ? BlastDamageShaman : BlastDamage;
            de.attackType = AttackTypes.Spell;
            de.ignoreInvuln = false;
            de.enabled = true;
            PlayAudio("Burst", 1.2f, 1.5f, 0.5f);

            return blast;
        }

        private void PlayAudio(string clip, float minPitch = 1f, float maxPitch = 1f, float volume = 1f, float delay = 0f)
        {
            IEnumerator Play()
            {
                AudioClip audioClip = PaleCourtCharms.Clips[clip];
                yield return new WaitForSeconds(delay);
                GameObject audioPlayerInstance = _audioPlayer.Spawn(transform.position, Quaternion.identity);
                AudioSource audio = audioPlayerInstance.GetComponent<AudioSource>();
                audio.outputAudioMixerGroup = HeroController.instance.GetComponent<AudioSource>().outputAudioMixerGroup;
                audio.pitch = Random.Range(minPitch, maxPitch);
                audio.volume = volume;
                audio.PlayOneShot(audioClip);
                yield return new WaitForSeconds(audioClip.length + 3f);
                Destroy(audioPlayerInstance);
            }
            GameManager.instance.StartCoroutine(Play());
        }
    }
}

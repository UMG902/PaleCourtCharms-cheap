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

        // Transcendence integration 
        private bool _transcChecked = false;
        private bool _transcAvailable = false;
        private object _shamanInstance = null;
        private MethodInfo _shamanEquippedMethod = null;
        private MethodInfo _shamanEnlargeStatic = null; 
        private Type _shamanType = null;

        private bool _shamanEquippedCached = false;

        private void OnEnable()
        {
            _spellControl = _hc.spellControl;

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
                dagger.FindGameObjectInChildren("Dribble L").layer = 9;
                dagger.FindGameObjectInChildren("Glow").layer = 9;
                dagger.FindGameObjectInChildren("Beam").layer = 9;
                DontDestroyOnLoad(dagger);
                PaleCourtCharms.preloadedGO["BoonDagger"] = dagger;
            }

            PaleCourtCharms.Clips["Burst"] = (AudioClip)_pvControl.GetAction<AudioPlayerOneShotSingle>("Focus Burst", 8).audioClip.Value;
            PaleCourtCharms.Clips["Plume Up"] = (AudioClip)_pvControl.GetAction<AudioPlayerOneShotSingle>("Plume Up", 1).audioClip.Value;

            ModifySpellFSM(true);

            //Update our cached flag whenever charms change:
            ModHooks.CharmUpdateHook += OnCharmUpdate;
            UpdateShamanAmpEquippedCache();
        }

        private void OnDisable()
        {
            ModifySpellFSM(false);
            ModHooks.CharmUpdateHook -= OnCharmUpdate;
        }

        private void OnCharmUpdate(PlayerData pd, HeroController hc)
        {
            UpdateShamanAmpEquippedCache();
        }

        private void UpdateShamanAmpEquippedCache()
        {
            if (!_transcChecked) TryInitTranscendenceIntegration();

            // If we don't have the equip method, we cannot check equip via Transcendence, so remain false.
            if (!_transcAvailable || _shamanEquippedMethod == null || _shamanType == null)
            {
                _shamanEquippedCached = false;
                return;
            }

            // Try to ensure we have a live Instance
            if (_shamanInstance == null && _shamanType != null)
            {
                try
                {
                    var instanceProp = _shamanType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (instanceProp != null)
                    {
                        _shamanInstance = instanceProp.GetValue(null);
                    }
                    else
                    {
                        var instanceField = _shamanType.GetField("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                        if (instanceField != null) _shamanInstance = instanceField.GetValue(null);
                    }
                }
                catch
                {
                    _shamanInstance = null;
                }
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
            catch
            {
                _shamanEquippedCached = false;
            }
        }

        private void ModifySpellFSM(bool enabled)
        {
            if (enabled)
            {
                _spellControl.ChangeTransition("Level Check 3", "LEVEL 1", "Scream Antic1 Blasts");
                _spellControl.ChangeTransition("Level Check 3", "LEVEL 2", "Scream Antic2 Blasts");

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
                _spellControl.ChangeTransition("Level Check 3", "LEVEL 1", "Scream Antic1");
                _spellControl.ChangeTransition("Level Check 3", "LEVEL 2", "Scream Antic2");

                _spellControl.ChangeTransition("Quake1 Down", "HERO LANDED", "Quake1 Land");
                _spellControl.ChangeTransition("Quake2 Down", "HERO LANDED", "Q2 Land");

                _spellControl.ChangeTransition("Level Check", "LEVEL 1", "Fireball 1");
                _spellControl.ChangeTransition("Level Check", "LEVEL 2", "Fireball 2");
            }
        }

        // Transcendence integration
        private void TryInitTranscendenceIntegration()
        {
            if (_transcChecked) return;
            _transcChecked = true;

            try
            {
                var modObj = ModHooks.GetMod("Transcendence");
                Assembly asm = null;
                if (modObj is Mod modInstance)
                {
                    asm = modInstance.GetType().Assembly;
                }
                else
                {
                    asm = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a =>
                        {
                            try { return a.GetType("Transcendence.ShamanAmp") != null; }
                            catch { return false; }
                        });
                }

                if (asm == null)
                {
                    _transcAvailable = false;
                    return;
                }

                _shamanType = asm.GetType("Transcendence.ShamanAmp");
                if (_shamanType == null)
                {
                    _transcAvailable = false;
                    return;
                }

                var instanceProp = _shamanType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProp != null)
                {
                    _shamanInstance = instanceProp.GetValue(null);
                }
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
            try
            {
                // static Enlarge is safe to call even if instance was null
                _shamanEnlargeStatic.Invoke(null, new object[] { obj });
            }
            catch
            {

            }
        }

        public void CastDaggers(bool upgraded)
        {
            bool shaman = PlayerData.instance.equippedCharm_19;
            int angleMin = shaman ? -30 : -25;
            int angleMax = shaman ? 30 : 25;
            int increment = shaman ? 20 : 25;
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
                float xVel = DaggerSpeed * Mathf.Cos(Mathf.Deg2Rad * angle) * -HeroController.instance.transform.localScale.x;
                float yVel = DaggerSpeed * Mathf.Sin(Mathf.Deg2Rad * angle);

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

            // Enemy iframes last 0.2s
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

            // Scale collider radius to match any transform scaling applied by Enlarge
            float scale = Math.Max(Mathf.Abs(blast.transform.localScale.x), Mathf.Abs(blast.transform.localScale.y));
            if (!float.IsNaN(scale) && scale > 0f)
            {
                col.radius *= scale;
            }

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

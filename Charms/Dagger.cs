using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PaleCourtCharms
{
    public class Dagger : MonoBehaviour
    {
        private const int DaggerDamage = 13;
        private const int DaggerDamageUpgraded = 25;
        public bool upgraded = false;
        public bool shamanEquipped = false;
        public bool snailEquipped = false;
        private readonly Dictionary<GameObject, Coroutine> _activeTargets = new Dictionary<GameObject, Coroutine>();
        private bool _toggleSource = false;

        private void OnDestroy()
        {
            foreach (var kv in new List<KeyValuePair<GameObject, Coroutine>>(_activeTargets))
            {
                var c = kv.Value;
                SafeStopCoroutine(c);
            }
            _activeTargets.Clear();
        }

        private void OnTriggerEnter2D(Collider2D collider)
        {
            int layer = collider.gameObject.layer;
            if (layer == 20 || layer == 9 || layer == 26 || layer == 31 || collider.CompareTag("Geo"))
            {
                return;
            }

            GameObject target = collider.gameObject;
            if (target == null) return;

            if (shamanEquipped || snailEquipped)
            {
                if (!_activeTargets.ContainsKey(target))
                {
                    IEnumerator routine = MultiHitTargetCoroutine(target);
                    if (routine == null) return;

                    Coroutine c = SafeStartCoroutine(routine);
                    if (c != null)
                    {
                        _activeTargets[target] = c;
                    }
                }
            }
            else
            {
                DoHit(target);
            }
        }

        private void OnTriggerExit2D(Collider2D collider)
        {
            GameObject target = collider.gameObject;
            if (target == null) return;

            if (_activeTargets.TryGetValue(target, out Coroutine c))
            {
                SafeStopCoroutine(c);
                _activeTargets.Remove(target);
            }
        }
        private Coroutine SafeStartCoroutine(IEnumerator routine)
        {
            if (routine == null) return null;
            if (!Application.isPlaying) return null;

            try
            {
                if (GameManager.instance != null)
                {
                    var gm = GameManager.instance;
                    if (gm != null && gm.gameObject != null)
                    {
                        try
                        {
                            return gm.StartCoroutine(routine);
                        }
                        catch
                        {

                        }
                    }
                }

                if (this != null && gameObject != null && enabled && gameObject.activeInHierarchy)
                {
                    try
                    {
                        return StartCoroutine(routine);
                    }
                    catch
                    {
                     
                        return null;
                    }
                }
            }
            catch
            {
         
            }

            return null;
        }

        private void SafeStopCoroutine(Coroutine c)
        {
            if (c == null) return;
            if (!Application.isPlaying) return;

            try
            {
                if (GameManager.instance != null)
                {
                    try
                    {
                        GameManager.instance.StopCoroutine(c);
                        return;
                    }
                    catch
                    {
                      
                    }
                }

                if (this != null)
                {
                    try
                    {
                        StopCoroutine(c);
                    }
                    catch
                    {
                 
                    }
                }
            }
            catch
            {
          
            }
        }
        private float GetMultiHitInterval()
        {
            if (shamanEquipped && snailEquipped) return 0.22f;
            if (shamanEquipped) return 0.20f;
            if (snailEquipped) return 0.24f;
            return 0.22f; 
        }

        private IEnumerator MultiHitTargetCoroutine(GameObject target)
        {
            if (target == null) yield break;

            GameObject keyRef = target;

            DoHit(target);

            float interval = GetMultiHitInterval();

            try
            {
                while (target != null)
                {
                    if (this == null) yield break;

                    yield return new WaitForSeconds(interval);

                    if (target == null) yield break;
                    if (this == null) yield break;

                    DoHit(target);

                    interval = GetMultiHitInterval();
                }
            }
            finally
            {
                try
                {
                    var keys = new List<GameObject>(_activeTargets.Keys);
                    foreach (var k in keys)
                    {
                        if (k == keyRef)
                        {
                            _activeTargets.Remove(k);
                            break;
                        }
                    }
                }
                catch
                {
                }
            }
        }

        private void DoHit(GameObject target)
        {
            if (target == null) return;

            HitInstance smallShotHit = new HitInstance();
            smallShotHit.DamageDealt = upgraded ? DaggerDamageUpgraded : DaggerDamage;
            smallShotHit.AttackType = AttackTypes.Spell;
            smallShotHit.IgnoreInvulnerable = true;
            smallShotHit.Source = _toggleSource ? gameObject : null;
            _toggleSource = !_toggleSource;

            smallShotHit.Multiplier = 1f;

            try
            {
                HitTaker.Hit(target, smallShotHit, 3);
            }
            catch
            {

            }
        }
    }
}



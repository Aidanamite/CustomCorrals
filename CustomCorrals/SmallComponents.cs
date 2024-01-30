using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace CustomCorrals
{
    public class DelaunchOnLeave : MonoBehaviour
    {
        void OnTriggerExit(Collider other)
        {
            var vac = other.GetComponent<VacDelaunchTrigger>();
            if (vac)
                vac.Delaunch();
        }
    }

    public class PlatformChecker : MonoBehaviour
    {
        CaveTrigger.Listener listener;
        List<PlatformComponent> covers = new List<PlatformComponent>();
        void Awake() => listener = gameObject.GetInterfaceComponent<CaveTrigger.Listener>();
        void Update()
        {
            if (listener != null)
            {
                var hits = Physics.RaycastAll(transform.position, new Vector3(0, -Mathf.Cos(SceneContext.Instance.TimeDirector.CurrDayFraction() * Mathf.PI * 2), -Mathf.Sin(SceneContext.Instance.TimeDirector.CurrDayFraction() * Mathf.PI * 2)), 100, 1);
                var newCovers = new List<PlatformComponent>();
                if (hits != null)
                    foreach (var hit in hits)
                    {
                        var p = hit.collider.GetComponentInParent<PlatformComponent>();
                        if (p)
                        {
                            newCovers.Add(p);
                            if (!covers.Contains(p))
                                listener.OnCaveEnter(p.gameObject, false, AmbianceDirector.Zone.DEFAULT);
                        }
                    }
                foreach (var p in covers)
                    if (p && !newCovers.Contains(p))
                        listener.OnCaveExit(p.gameObject, false, AmbianceDirector.Zone.DEFAULT);
                covers = newCovers;
            }
        }
    }

    public class ModeOption : DroneMetadata.Program.BaseComponent
    {
        System.Action onClick;
        public ModeOption(Sprite Icon, string NameKey, System.Action OnSelect)
        {
            id = NameKey;
            image = Icon;
            onClick = OnSelect;
        }
        public void Selected() => onClick?.Invoke();
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{

    [CreateAssetMenu(fileName = "New Particle Command", menuName = "SAS/DeveloperConsole/Commands/Particle Command")]
    public class ParticleCommand : CompositeConsoleCommand
    {
        private class ParticleBackupState
        {
            public ParticleSystem ps;
            public bool wasActive;
        }


        [SerializeField] private GameObject m_ParticleStatsPrefab;
        private GameObject _particleStatsInstance;
        private readonly List<ParticleBackupState> _backupStates = new();
        private bool _isCulled = false;

        public override string HelpText => "";

        protected override void CommandMethodRegistry()
        {
            Register("ShowStats", ShowStats);
            Register("Refresh", Refresh);
            Register("CullOffscreen", CullOffscreen);
            Register("ResetCull", ResetCull);
            Register("SetAll", ToggleAll);
        }

        private bool ShowStats(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                if (BoolUtil.TryParse(args[0], out var isVisible))
                {
                    if (_particleStatsInstance == null)
                    {
                        _particleStatsInstance = Instantiate(m_ParticleStatsPrefab);
                        _particleStatsInstance.name = "ParticleStatsUI";
                    }

                    _particleStatsInstance.SetActive(isVisible);
                    return true;
                }
            }

            return false;
        }

        private bool Refresh(string[] args)
        {

            if (_particleStatsInstance == null)
            {
                _particleStatsInstance = Instantiate(m_ParticleStatsPrefab);
                _particleStatsInstance.name = "ParticleStatsUI";
            }

            _particleStatsInstance.SetActive(false);
            _particleStatsInstance.SetActive(true);
            Debug.Log("Particle Stats UI Refreshed.");

            return true;
        }

        private bool CullOffscreen(string[] args)
        {
            Camera cam = Camera.main;
            if (!cam)
            {
                Debug.LogError("ParticleCommand: No Main Camera found!");
                return false;
            }

            _backupStates.Clear();

            ParticleSystem[] systems = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int culled = 0;

            foreach (var ps in systems)
            {
                if (!ps) continue;

                var psRenderer = ps.GetComponent<ParticleSystemRenderer>();
                if (!psRenderer) continue;

                bool visible = IsVisible(cam, psRenderer);

                // Backup original state
                _backupStates.Add(new ParticleBackupState
                {
                    ps = ps,
                    wasActive = ps.gameObject.activeSelf
                });

                if (!visible)
                {
                    ps.gameObject.SetActive(false);
                    culled++;
                }
            }

            _isCulled = true;
            Debug.Log($"Particle Cull: Disabled {culled} particle GameObjects (outside frustum).");
            return true;
        }

        private bool IsVisible(Camera cam, ParticleSystemRenderer renderer)
        {
            var planes = GeometryUtility.CalculateFrustumPlanes(cam);
            return GeometryUtility.TestPlanesAABB(planes, renderer.bounds);
        }

        private bool ResetCull(string[] args)
        {
            if (!_isCulled || _backupStates.Count == 0)
            {
                Debug.LogWarning("Particle ResetCull: Nothing to restore. Run CullOffscreen first.");
                return true;
            }

            foreach (var entry in _backupStates)
            {
                if (!entry.ps) continue;

                entry.ps.gameObject.SetActive(entry.wasActive);
            }

            _backupStates.Clear();
            _isCulled = false;

            Debug.Log("Particle ResetCull: GameObject states restored.");
            return true;
        }

        private bool ToggleAll(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                if (BoolUtil.TryParse(args[0], out var turnOn))
                {
                    ParticleSystem[] systems = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);

                    int affected = 0;

                    foreach (var ps in systems)
                    {
                        if (!ps) continue;

                        // Backup state only once
                        if (!_backupStates.Exists(e => e.ps == ps))
                        {
                            _backupStates.Add(new ParticleBackupState
                            {
                                ps = ps,
                                wasActive = ps.gameObject.activeSelf
                            });
                        }

                        if (ps.gameObject.activeSelf != turnOn)
                        {
                            ps.gameObject.SetActive(turnOn);
                            affected++;
                        }
                    }

                    _isCulled = !turnOn;

                    Debug.Log($"ToggleAll: {(turnOn ? "Enabled" : "Disabled")} {affected}/{systems.Length} particle GameObjects.");
                    return true;
                }
            }

            Debug.LogError("ToggleAll: Invalid parameter. Usage: ToggleAll on/off or true/false");
            return false;
        }
    }
}

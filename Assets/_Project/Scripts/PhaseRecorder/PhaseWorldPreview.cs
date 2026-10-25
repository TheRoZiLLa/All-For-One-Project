using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Recorded.Phase
{
    // The recording is a plan: world consequences are applied only by playback.
    internal sealed class PhaseWorldPreview
    {
        private readonly Dictionary<Renderer, Material[]> _materials = new Dictionary<Renderer, Material[]>();
        private readonly List<(Rigidbody body, bool kinematic, Vector3 velocity, Vector3 spin)> _bodies = new List<(Rigidbody, bool, Vector3, Vector3)>();
        private readonly List<(NavMeshAgent agent, bool stopped)> _agents = new List<(NavMeshAgent, bool)>();
        private readonly List<(Animator animator, float speed)> _animators = new List<(Animator, float)>();
        private Material _hologram;

        public void Enter(Transform player, Material hologram)
        {
            _hologram = hologram;
            if (hologram != null)
                foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (renderer.transform.IsChildOf(player) || renderer is ParticleSystemRenderer || renderer is LineRenderer) continue;
                    var original = renderer.sharedMaterials;
                    _materials.Add(renderer, original);
                    var replacement = new Material[original.Length];
                    for (int i = 0; i < replacement.Length; i++) replacement[i] = _hologram;
                    renderer.sharedMaterials = replacement;
                }
            foreach (var body in Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
            {
                if (body.transform.IsChildOf(player)) continue;
                _bodies.Add((body, body.isKinematic, body.linearVelocity, body.angularVelocity));
                body.isKinematic = true;
            }
            foreach (var agent in Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
            {
                if (agent.transform.IsChildOf(player) || !agent.isActiveAndEnabled || !agent.isOnNavMesh) continue;
                _agents.Add((agent, agent.isStopped));
                agent.isStopped = true;
            }
            foreach (var animator in Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
            {
                if (animator.transform.IsChildOf(player)) continue;
                _animators.Add((animator, animator.speed));
                animator.speed = 0f;
            }
        }

        public void Exit()
        {
            foreach (var item in _materials) if (item.Key != null) item.Key.sharedMaterials = item.Value;
            foreach (var item in _bodies)
            {
                if (item.body == null) continue;
                item.body.isKinematic = item.kinematic;
                if (!item.kinematic) { item.body.linearVelocity = item.velocity; item.body.angularVelocity = item.spin; }
            }
            foreach (var item in _agents)
                if (item.agent != null && item.agent.isActiveAndEnabled && item.agent.isOnNavMesh) item.agent.isStopped = item.stopped;
            foreach (var item in _animators) if (item.animator != null) item.animator.speed = item.speed;
            _materials.Clear(); _bodies.Clear(); _agents.Clear(); _animators.Clear();
        }
    }
}

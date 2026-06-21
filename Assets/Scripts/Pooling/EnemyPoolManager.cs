using System.Collections.Generic;
using UnityEngine;

namespace Wavekeep.Pooling
{
    /// <summary>
    /// Generic GameObject pool for 3D enemy prefabs (CLAUDE.md §3.5 — pooling from day one,
    /// no Instantiate/Destroy in steady-state gameplay). A plain C# class owned by
    /// <see cref="Wavekeep.Core.GameSession"/>.
    ///
    /// Because this is a 3D game, <see cref="Get"/>/<see cref="Release"/> reset transform
    /// (position/rotation/scale) and physics (linear/angular velocity) state on reuse — not
    /// just <c>SetActive</c> (CLAUDE.md §3.7). No enemy-specific logic lives here (that is
    /// Task 02); this is the reusable mechanism only.
    /// </summary>
    public sealed class EnemyPoolManager
    {
        private readonly Transform _root;

        // Per-prefab stacks of inactive, ready-to-reuse instances.
        private readonly Dictionary<GameObject, Stack<GameObject>> _available =
            new Dictionary<GameObject, Stack<GameObject>>();

        // Reverse lookup so Release knows which pool an instance belongs to.
        private readonly Dictionary<GameObject, GameObject> _instanceToPrefab =
            new Dictionary<GameObject, GameObject>();

        public EnemyPoolManager(Transform root)
        {
            _root = root;
        }

        /// <summary>Pre-instantiate <paramref name="count"/> inactive copies of <paramref name="prefab"/>.</summary>
        public void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0) return;

            var stack = GetOrCreateStack(prefab);
            for (int i = 0; i < count; i++)
            {
                var instance = CreateInstance(prefab);
                instance.SetActive(false);
                stack.Push(instance);
            }
        }

        /// <summary>
        /// Returns an active instance of <paramref name="prefab"/>, reusing a pooled one when
        /// available or creating a new one otherwise. Transform and physics state are reset.
        /// </summary>
        public GameObject Get(GameObject prefab)
        {
            if (prefab == null) return null;

            var stack = GetOrCreateStack(prefab);
            var instance = stack.Count > 0 ? stack.Pop() : CreateInstance(prefab);

            ResetTransform(instance, prefab);
            ResetPhysics(instance);
            instance.SetActive(true);
            return instance;
        }

        /// <summary>Deactivates <paramref name="instance"/>, resets its physics, and returns it to its pool.</summary>
        public void Release(GameObject instance)
        {
            if (instance == null) return;

            if (!_instanceToPrefab.TryGetValue(instance, out var prefab))
            {
                // Not owned by this pool — deactivate defensively but do not pool it.
                instance.SetActive(false);
                return;
            }

            ResetPhysics(instance);
            instance.SetActive(false);
            if (_root != null) instance.transform.SetParent(_root, false);

            GetOrCreateStack(prefab).Push(instance);
        }

        /// <summary>Destroys all pooled instances and clears bookkeeping. Called on session teardown.</summary>
        public void Clear()
        {
            foreach (var stack in _available.Values)
            {
                while (stack.Count > 0)
                {
                    var obj = stack.Pop();
                    if (obj != null) Object.Destroy(obj);
                }
            }

            _available.Clear();
            _instanceToPrefab.Clear();
        }

        private Stack<GameObject> GetOrCreateStack(GameObject prefab)
        {
            if (!_available.TryGetValue(prefab, out var stack))
            {
                stack = new Stack<GameObject>();
                _available[prefab] = stack;
            }

            return stack;
        }

        private GameObject CreateInstance(GameObject prefab)
        {
            var instance = _root != null
                ? Object.Instantiate(prefab, _root)
                : Object.Instantiate(prefab);

            _instanceToPrefab[instance] = prefab;
            return instance;
        }

        private static void ResetTransform(GameObject instance, GameObject prefab)
        {
            var t = instance.transform;
            var p = prefab.transform;
            t.SetPositionAndRotation(p.position, p.rotation);
            t.localScale = p.localScale;
        }

        private static void ResetPhysics(GameObject instance)
        {
            // Skip kinematic bodies: Unity warns if you set velocity on them, and they have none.
            if (instance.TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic)
            {
                // Unity 6: Rigidbody.velocity is renamed to linearVelocity.
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }
}

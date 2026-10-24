using UnityEngine;

namespace Recorded.Enemies
{
    /// <summary>Prototype hit pulse; applies a small physical shove because the project has no shared player-health contract.</summary>
    public sealed class SurveyorBolt : MonoBehaviour
    {
        [SerializeField] private float speed = 18f;
        [SerializeField] private float impulse = 5f;
        [SerializeField] private float lifetime = 4f;

        private Vector3 _direction;
        private Transform _ownerRoot;
        private Rigidbody _body;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            if (_body == null) _body = gameObject.AddComponent<Rigidbody>();
            _body.isKinematic = true;
        }

        public void Launch(Vector3 direction, Transform owner = null)
        {
            _direction = direction.normalized;
            _ownerRoot = owner != null ? owner.root : null;
            Destroy(gameObject, lifetime);
        }

        private void FixedUpdate() => _body.MovePosition(_body.position + _direction * (speed * Time.fixedDeltaTime));

        private void OnTriggerEnter(Collider other)
        {
            if (other.transform.root == transform.root || other.transform.root == _ownerRoot) return;
            Rigidbody targetBody = other.attachedRigidbody;
            if (targetBody != null && !targetBody.isKinematic)
                targetBody.AddForce(_direction * impulse, ForceMode.Impulse);
            else
            {
                CharacterController character = other.GetComponentInParent<CharacterController>();
                if (character != null) character.Move(_direction * 0.25f);
            }
            Destroy(gameObject);
        }
    }
}

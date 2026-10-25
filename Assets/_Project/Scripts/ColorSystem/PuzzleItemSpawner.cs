using UnityEngine;

namespace Recorded.ColorSystem
{
    /// <summary>
    /// Fall policy for systemic props.
    /// Puzzle-critical bodies return home. Ordinary props are removed once they drop out of the level.
    /// </summary>
    [DisallowMultipleComponent]
    public class PuzzleItemSpawner : MonoBehaviour
    {
        [SerializeField] private float fallDestroyY = -8f;
        [SerializeField] private bool forcePuzzleCritical;

        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private Rigidbody _body;
        private IColorTarget _colorTarget;
        private SpectrumSkillResponder _skill;

        private void Awake()
        {
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
            _body = GetComponent<Rigidbody>();
            _colorTarget = GetComponent<IColorTarget>();
            _skill = GetComponent<SpectrumSkillResponder>();
        }

        private void LateUpdate()
        {
            if (Recorded.Phase.PhaseRecorder.IsWorldPaused) return;
            if (!Application.isPlaying) return;
            if (transform.position.y >= fallDestroyY) return;

            if (IsCritical)
            {
                RespawnHome();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private bool IsCritical => forcePuzzleCritical || (_colorTarget != null && _colorTarget.IsPuzzleCritical);

        private void RespawnHome()
        {
            if (_skill != null && _skill.IsBlueStasis)
            {
                _skill.DeactivateCyanStasis(restoreSnapshotVelocity: false);
            }

            _colorTarget?.ResetColors();

            if (_body != null)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
                _body.position = _homePosition;
                _body.rotation = _homeRotation;
            }

            transform.SetPositionAndRotation(_homePosition, _homeRotation);
        }
    }
}

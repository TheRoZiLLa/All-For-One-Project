using System.Collections;
using UnityEngine;

namespace Recorded.Testing
{
    /// <summary>
    /// Simple test dummy target that responds with a visual flash when hit by the Spectrum Gun.
    /// Does not implement health or damage yet, strictly for verification of raycast hit detection.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TestTarget : MonoBehaviour
    {
        [SerializeField] private Color hitFlashColor = Color.white;
        [SerializeField] private float flashDuration = 0.1f;

        private Renderer _renderer;
        private Color _originalColor;
        private Material _instancedMat;
        private Coroutine _flashCoroutine;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            if (_renderer != null)
            {
                _instancedMat = _renderer.material;
                _originalColor = _instancedMat.color;
            }
        }

        public void NotifyHit(RaycastHit hitInfo)
        {
            if (_renderer == null || _instancedMat == null) return;

            if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
            _flashCoroutine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            _instancedMat.color = hitFlashColor;
            yield return new WaitForSeconds(flashDuration);
            _instancedMat.color = _originalColor;
            _flashCoroutine = null;
        }

        private void OnDestroy()
        {
            if (_instancedMat != null)
            {
                Destroy(_instancedMat);
            }
        }
    }
}

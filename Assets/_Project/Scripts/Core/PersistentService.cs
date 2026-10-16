using UnityEngine;

namespace MafiaNight.Core
{
    /// <summary>
    /// Base class for services that live on a root GameObject in the Bootstrap scene and
    /// survive scene loads. Registers itself in <see cref="ServiceLocator"/> as <typeparamref name="TService"/>.
    /// A duplicate instance (e.g. Bootstrap loaded twice) destroys itself.
    /// </summary>
    public abstract class PersistentService<TService> : MonoBehaviour where TService : class
    {
        private bool _isRegisteredInstance;

        protected virtual void Awake()
        {
            var self = this as TService;
            if (self == null)
            {
                Log.Error($"{GetType().Name} does not implement {typeof(TService).Name}", "PersistentService");
                return;
            }

            if (ServiceLocator.IsRegistered<TService>())
            {
                Destroy(gameObject);
                return;
            }

            ServiceLocator.Register(self);
            _isRegisteredInstance = true;

            if (transform.parent == null) DontDestroyOnLoad(gameObject);

            OnServiceAwake();
        }

        /// <summary>Use instead of Awake in derived classes.</summary>
        protected virtual void OnServiceAwake() { }

        protected virtual void OnDestroy()
        {
            if (_isRegisteredInstance) ServiceLocator.Unregister(this as TService);
        }
    }
}

using System;
using System.Collections.Generic;

namespace MafiaNight.Core
{
    /// <summary>
    /// Central registry for game services.
    /// Services register themselves by interface (e.g. ICurrencyService) so consumers
    /// depend on the contract, not on a concrete MonoBehaviour or a static Instance.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public static void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));

            var type = typeof(T);
            if (_services.ContainsKey(type))
            {
                Log.Warning($"Service {type.Name} already registered - replacing it", "ServiceLocator");
            }
            _services[type] = service;
        }

        /// <summary>Removes the service only if <paramref name="service"/> is the registered instance.</summary>
        public static void Unregister<T>(T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var current) && ReferenceEquals(current, service))
            {
                _services.Remove(typeof(T));
            }
        }

        public static bool IsRegistered<T>() where T : class => _services.ContainsKey(typeof(T));

        public static bool TryGet<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var obj))
            {
                service = (T)obj;
                return true;
            }
            service = null;
            return false;
        }

        /// <summary>Returns the service or throws if it was never registered (fail fast on wiring bugs).</summary>
        public static T Get<T>() where T : class
        {
            if (TryGet<T>(out var service)) return service;
            throw new InvalidOperationException(
                $"[ServiceLocator] {typeof(T).Name} is not registered. Did the game start from the Bootstrap scene?");
        }

        /// <summary>Clears all services. Called on domain reload / play mode start.</summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset() => _services.Clear();
    }
}

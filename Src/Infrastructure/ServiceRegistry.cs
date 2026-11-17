using System;
using System.Collections.Generic;

namespace Ludens.Infrastructure
{
    /// <summary>Small composition container used instead of a DI package.</summary>
    public sealed class ServiceRegistry
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public void AddSingleton<T>(T instance) where T : class
        {
            if (instance == null) throw new ArgumentNullException("instance");
            _services[typeof(T)] = instance;
        }

        public T GetRequired<T>() where T : class
        {
            object service;
            if (!_services.TryGetValue(typeof(T), out service))
                throw new InvalidOperationException("Service not registered: " + typeof(T).FullName);
            return (T)service;
        }
    }
}
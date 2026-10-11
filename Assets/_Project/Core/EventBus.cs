using System;
using System.Collections.Generic;

namespace Myrmutation.Core
{
    /// <summary>
    /// Bus de eventos global. Suscribirse en OnEnable y desuscribirse en OnDisable.
    /// Uso: EventBus.Subscribe&lt;AntDied&gt;(OnAntDied); EventBus.Publish(new AntDied(ant, cause));
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> Handlers = new Dictionary<Type, Delegate>();

        public static void Subscribe<T>(Action<T> handler)
        {
            Handlers.TryGetValue(typeof(T), out var existing);
            Handlers[typeof(T)] = Delegate.Combine(existing, handler);
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            if (!Handlers.TryGetValue(typeof(T), out var existing)) return;
            var result = Delegate.Remove(existing, handler);
            if (result == null) Handlers.Remove(typeof(T));
            else Handlers[typeof(T)] = result;
        }

        public static void Publish<T>(T evt)
        {
            if (Handlers.TryGetValue(typeof(T), out var d)) ((Action<T>)d)?.Invoke(evt);
        }
    }
}

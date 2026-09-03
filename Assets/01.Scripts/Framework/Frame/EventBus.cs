using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 타입 기반 publish/subscribe 이벤트 허브입니다.
/// </summary>
public class EventBus
{
    private static readonly Lazy<EventBus> _instance = new Lazy<EventBus>(() => new EventBus());
    public static EventBus Instance => _instance.Value;

    private readonly Dictionary<Type, Delegate> _events = new Dictionary<Type, Delegate>();

    private EventBus() { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSubscriptions()
    {
        Instance._events.Clear();
    }

    public void Subscribe<T>(Action<T> onEvent)
    {
        Type eventType = typeof(T);
        if (_events.TryGetValue(eventType, out Delegate existingDelegate))
        {
            _events[eventType] = Delegate.Combine(existingDelegate, onEvent);
        }
        else
        {
            _events[eventType] = onEvent;
        }
    }

    public void Unsubscribe<T>(Action<T> onEvent)
    {
        Type eventType = typeof(T);
        if (!_events.TryGetValue(eventType, out Delegate existingDelegate))
        {
            return;
        }

        Delegate remainingSubscribers = Delegate.Remove(existingDelegate, onEvent);
        if (remainingSubscribers == null)
        {
            _events.Remove(eventType);
            return;
        }

        _events[eventType] = remainingSubscribers;
    }

    public void Publish<T>(T eventMessage)
    {
        Type eventType = typeof(T);
        if (!_events.TryGetValue(eventType, out Delegate existingDelegate))
        {
            return;
        }

        foreach (Action<T> subscriber in existingDelegate.GetInvocationList())
        {
            // 하나의 구독자가 실패해도 나머지 구독자는 알림을 받습니다.
            try
            {
                subscriber(eventMessage);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}

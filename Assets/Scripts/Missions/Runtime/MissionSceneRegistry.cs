using System.Collections.Generic;
using ShinyMinds.Missions.Data;
using UnityEngine;

namespace ShinyMinds.Missions.Runtime
{
    /// <summary>
    /// Maps string keys to scene objects.
    ///
    /// ScriptableObjects cannot hold scene references, and GameObject.Find is unusable
    /// in this project: the scene has ~325 objects called "Waypoint (n)" and three
    /// called "GameManager". Components register themselves here instead.
    /// </summary>
    public static class MissionSceneRegistry
    {
        static readonly Dictionary<string, IMissionActor> actors = new Dictionary<string, IMissionActor>();
        static readonly Dictionary<string, Transform> markers = new Dictionary<string, Transform>();

        public static void Register(IMissionActor actor)
        {
            if (actor == null || string.IsNullOrEmpty(actor.ActorKey))
                return;

            if (actors.TryGetValue(actor.ActorKey, out IMissionActor existing) && existing != actor)
                Debug.LogWarning($"MissionSceneRegistry: duplicate actor key '{actor.ActorKey}'. The later registration wins.", actor.GameObject);

            actors[actor.ActorKey] = actor;
        }

        public static void Unregister(IMissionActor actor)
        {
            if (actor == null || string.IsNullOrEmpty(actor.ActorKey))
                return;

            if (actors.TryGetValue(actor.ActorKey, out IMissionActor existing) && existing == actor)
                actors.Remove(actor.ActorKey);
        }

        public static void RegisterMarker(string key, Transform marker)
        {
            if (string.IsNullOrEmpty(key) || marker == null)
                return;

            if (markers.TryGetValue(key, out Transform existing) && existing != marker)
                Debug.LogWarning($"MissionSceneRegistry: duplicate marker key '{key}'. The later registration wins.", marker);

            markers[key] = marker;
        }

        public static void UnregisterMarker(string key, Transform marker)
        {
            if (string.IsNullOrEmpty(key))
                return;

            if (markers.TryGetValue(key, out Transform existing) && existing == marker)
                markers.Remove(key);
        }

        public static IMissionActor GetActor(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            return actors.TryGetValue(key, out IMissionActor a) ? a : null;
        }

        public static Transform GetMarker(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            return markers.TryGetValue(key, out Transform m) ? m : null;
        }

        public static IEnumerable<IMissionActor> AllActors() => actors.Values;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            actors.Clear();
            markers.Clear();
        }
    }
}

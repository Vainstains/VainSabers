using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Component = UnityEngine.Component;
using Object = UnityEngine.Object;

namespace VainSabers.Helpers
{
    public static class ComponentExtensions
    {
        public static bool TryRequireComponent<TComponent>(this GameObject go, out TComponent result, bool addIfMissing = true) where TComponent : Component
        {
            result = go.GetComponent<TComponent>();
            if (addIfMissing && result == null)
                result = go.AddComponent<TComponent>();
            
            return result != null;
        }
        
        public static bool TryRequireComponentInParent<TComponent>(this GameObject go, out TComponent result) where TComponent : Component
        {
            result = go.GetComponentInParent<TComponent>();
            return result != null;
        }
        
        public static bool TryRequireComponentInChildren<TComponent>(this GameObject go, out TComponent result) where TComponent : Component
        {
            result = go.GetComponentInChildren<TComponent>();
            return result != null;
        }

        public static TComponent RequireComponent<TComponent>(this GameObject go) where TComponent : Component
        {
            var ok = TryRequireComponent<TComponent>(go, out var result);
            
            Debug.Assert(ok, $"The required component {typeof(TComponent)} was not found and somehow was not created successfully.");

            return result;
        }
    }


    public enum ComponentLocation
    {
        InSelf,
        InChildren,
        InParent
    }
    
    [AttributeUsage(AttributeTargets.Field)]
    public class FindComponentAttribute : Attribute
    {
        public ComponentLocation Location { get; private set; }

        public FindComponentAttribute(ComponentLocation location = ComponentLocation.InSelf)
        {
            Location = location;
        }
    }

    public class RequiredComponentAttribute : FindComponentAttribute
    {
        public RequiredComponentAttribute(ComponentLocation location = ComponentLocation.InSelf) : base(location) { }
    }

    public static class ComponentInjectionExtension
    {
        private interface IInjectionEntry
        {
            bool TryInject(object instance);
        }

        private class InjectFromSelfEntry : IInjectionEntry
        {
            private readonly FieldInfo m_fieldInfo;
            private readonly bool m_addIfMissing;
    
            public InjectFromSelfEntry(FieldInfo fieldInfo, bool addIfMissing)
            {
                m_fieldInfo = fieldInfo;
                m_addIfMissing = addIfMissing;
            }
    
            public bool TryInject(object instance)
            {
                if (instance is not MonoBehaviour mb) return false;
                var currentValue = m_fieldInfo.GetValue(mb) as Object;
                if (currentValue != null) return true;
    
                var comp = mb.GetComponent(m_fieldInfo.FieldType);
                if (comp == null && m_addIfMissing)
                {
                    comp = mb.gameObject.AddComponent(m_fieldInfo.FieldType);
                }
    
                if (comp != null)
                {
                    m_fieldInfo.SetValue(mb, comp);
                    return true;
                }
    
                return false;
            }
        }

        private class InjectFromChildEntry : IInjectionEntry
        {
            private readonly FieldInfo m_fieldInfo;
    
            public InjectFromChildEntry(FieldInfo fieldInfo)
            {
                m_fieldInfo = fieldInfo;
            }
    
            public bool TryInject(object instance)
            {
                if (instance is not MonoBehaviour mb) return false;
                var currentValue = m_fieldInfo.GetValue(mb) as Object;
                if (currentValue != null) return true;
    
                var comp = mb.GetComponentInChildren(m_fieldInfo.FieldType, true);
                if (comp != null)
                {
                    // Debug.Log($"Injecting existing {m_fieldInfo.FieldType.Name} from \"{comp.gameObject.name}\" (child of \"{mb.gameObject.name}\")");
                    m_fieldInfo.SetValue(mb, comp);
                    return true;
                }
    
                return false;
            }
        }
    
        private class InjectFromParentEntry : IInjectionEntry
        {
            private readonly FieldInfo m_fieldInfo;
    
            public InjectFromParentEntry(FieldInfo fieldInfo)
            {
                m_fieldInfo = fieldInfo;
            }
    
            public bool TryInject(object instance)
            {
                if (instance is not MonoBehaviour mb) return false;
                var currentValue = m_fieldInfo.GetValue(mb) as Object;
                if (currentValue != null) return true;
    
                var comp = mb.GetComponentInParent(m_fieldInfo.FieldType);
                if (comp != null)
                {
                    // Debug.Log($"Injecting existing {m_fieldInfo.FieldType.Name} from \"{comp.gameObject.name}\" (parent of \"{mb.gameObject.name}\")");
                    m_fieldInfo.SetValue(mb, comp);
                    return true;
                }
    
                return false;
            }
        }
        
        private static readonly Dictionary<Type, IInjectionEntry[]> InjectionEntryCache = new Dictionary<Type, IInjectionEntry[]>();

        /// <summary>
        /// Automatically injects all private component fields that are null, and that are also marked with FindComponent or RequireComponent. Can be called anytime.
        /// </summary>
        /// <returns>True if every field marked with FindComponent or RequireComponent is non-null.</returns>
        public static bool Inject<TBehaviour>(this TBehaviour self, ref bool alreadyInjected) where TBehaviour : MonoBehaviour
        {
            if (alreadyInjected)
                return true;

            alreadyInjected = self.Inject();
            return alreadyInjected;
        }
        public static bool Inject<TBehaviour>(this TBehaviour self) where TBehaviour : MonoBehaviour
        {
            var type = typeof(TBehaviour);
            IInjectionEntry[] injectionEntries;
            if (!InjectionEntryCache.TryGetValue(type, out injectionEntries))
            {
                var entries = new List<IInjectionEntry>();
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                     BindingFlags.NonPublic))
                {
                    if (field.GetCustomAttribute(typeof(FindComponentAttribute), true) is not FindComponentAttribute attr)
                        continue;
                    
                    var require = attr is RequiredComponentAttribute;
                    var location = attr.Location;

                    switch (location)
                    {
                        case ComponentLocation.InSelf:
                            entries.Add(new InjectFromSelfEntry(field, require));
                            break;
                        case ComponentLocation.InParent:
                            entries.Add(new InjectFromParentEntry(field));
                            break;
                        case ComponentLocation.InChildren:
                            entries.Add(new InjectFromChildEntry(field));
                            break;
                    }
                }
                
                injectionEntries = entries.ToArray();
                InjectionEntryCache.Add(type, entries.ToArray());
            }
            
            var allOk = true;
            foreach (var entry in injectionEntries)
            {
                allOk &= entry.TryInject(self);
            }
            
            return allOk;
        }
    }
}
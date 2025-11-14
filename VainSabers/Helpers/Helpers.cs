using System;
using System.Reflection;
using UnityEngine;

namespace VainSabers.Helpers;

public static class Helpers
{
    public static TComponent AddInitComponent<TComponent>(
        this GameObject self, 
        params object[] args
    ) where TComponent : Component
    {
        var comp = self.AddComponent<TComponent>();
        
        var method = typeof(TComponent).GetMethod(
            "Init",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (method != null)
        {
            try
            {
                method.Invoke(comp, args);
            }
            catch (TargetParameterCountException)
            {
                Plugin.Log.Error(
                    $"Init(...) on {typeof(TComponent).Name} expects {method?.GetParameters().Length} parameters, " +
                    $"but {args.Length} were provided."
                );
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Init(...) invocation on {typeof(TComponent).Name} failed: {ex}");
            }
        }

        return comp;
    }
    
    public static TComponent AddInitChild<TComponent>(
        this GameObject self, 
        params object[] args
    ) where TComponent : Component
    {
        var childGo = new GameObject(typeof(TComponent).Name);
        childGo.transform.SetParent(self.transform, false);

        return childGo.AddInitComponent<TComponent>(args);
    }
}
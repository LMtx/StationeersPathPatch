using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Assets.Scripts;
using Assets.Scripts.Serialization;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace PathPatch
{
  [BepInPlugin("pathpatch", "Path Patch", "0.1.1")]
  class PathPatchMod : BaseUnityPlugin
  {
    public static ConfigEntry<bool> LogToConsole;

    public void Awake()
    {
      if (Harmony.HasAnyPatches("PathPatch"))
        return;

      LogToConsole = Config.Bind(
        new("Logging", "LogToConsole"),
        false,
        new("When enabled, logs directory delete errors to the in-game console")
      );

      var harmony = new Harmony("PathPatch");
      harmony.PatchAll();
    }
  }

  [HarmonyPatch]
  static class DebugPatches
  {
    [HarmonyTargetMethod]
    static MethodInfo TargetMethod()
    {
      var method = typeof(LoadHelper).GetMethod("LoadWorldTask", BindingFlags.Static | BindingFlags.NonPublic);
      var attr = method.GetCustomAttribute<AsyncStateMachineAttribute>();
      return attr.StateMachineType.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> LoadHelper_LoadWorldTask(IEnumerable<CodeInstruction> instructions)
    {
      var matcher = new CodeMatcher(instructions);
      matcher.MatchStartForward(CodeInstruction.Call(() => Directory.Delete("", true)));
      if (matcher.IsInvalid)
        throw new Exception("could not find LoadWorldTask insertion point");

      matcher.RemoveInstruction();
      matcher.InsertAndAdvance(CodeInstruction.Call(() => TryDelete(default, default)));

      return matcher.Instructions();
    }

    static void LogError(Exception ex)
    {
      Debug.Log($"Ignoring delete error: {ex.Message}\n{ex.StackTrace}");
      if (PathPatchMod.LogToConsole.Value)
        ConsoleWindow.PrintAction($"{ex.Message}\n{ex.StackTrace}");
    }

    static void TryDelete(string path, bool recurse)
    {
      try
      {
        Directory.Delete(path, recurse);
      }
      catch (Exception ex)
      {
        LogError(ex);
      }
    }
  }
}
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using UnityEngine;

namespace OrbusQuestHelper;

/// <summary>
/// One-shot, opt-in, READ-ONLY capture of original Missions HTML. This
/// component cannot call appendChild, removeChild, set_innerHTML or modify
/// missions. It runs only when explicitly enabled in BepInEx config.
/// </summary>
public sealed class MissionHtmlCapture : MonoBehaviour
{
    public MissionHtmlCapture(IntPtr pointer) : base(pointer) { }

    private const float PollSeconds = 2.5f;
    private const int MaxHtmlCharacters = 700000;
    private float _nextPoll;
    private int _lastFingerprint;
    private int _lastIconCount;
    private int _stableSamples;
    private bool _finished;
    private bool _reportedMissing;
    private bool _reportedError;

    private void Update()
    {
        if (_finished || Time.realtimeSinceStartup < _nextPoll) return;
        _nextPoll = Time.realtimeSinceStartup + PollSeconds;

        try
        {
            object? doc = PowerUiRuntime.PlayerMenuDocument();
            if (doc == null) return;
            object? missionContainer =
                PowerUiRuntime.ById(doc, "missions-container");
            if (missionContainer == null)
            {
                if (!_reportedMissing)
                {
                    _reportedMissing = true;
                    Plugin.Logger.LogInfo("[QuestHelper] HTML capture waiting for " +
                        "native missions-container. No UI changes made.");
                }
                return;
            }

            // Pure read: this is the game's existing PowerUI element
            // get_innerHTML() method. No setter or DOM mutation is called.
            string html = PowerUiRuntime.Text(missionContainer, "innerHTML");
            if (html.Length < 128 || html.Length > MaxHtmlCharacters)
                return;
            int iconCount = MissionMarkupCounter.Count(html);
            if (iconCount < 10) return; // Don't save the empty startup tab.

            int fingerprint = StringComparer.Ordinal.GetHashCode(html);
            if (fingerprint == _lastFingerprint && iconCount == _lastIconCount)
                _stableSamples++;
            else
            {
                _lastFingerprint = fingerprint;
                _lastIconCount = iconCount;
                _stableSamples = 1;
            }
            // Wait until the same populated page is seen across three polls
            // before capturing, avoiding partial mission-load snapshots.
            if (_stableSamples < 3) return;

            string directory = Path.Combine(Paths.ConfigPath,
                "OrbusQuestHelper", "Diagnostics");
            Directory.CreateDirectory(directory);
            string filename = DateTime.Now.ToString("yyyyMMdd_HHmmss") +
                "_MissionsOriginal.html";
            string fullPath = Path.Combine(directory, filename);
            File.WriteAllText(fullPath, html, new UTF8Encoding(false));
            _finished = true;
            // BepInEx saves this setting, preventing a new capture on every
            // subsequent game launch unless the user explicitly opts in.
            Plugin.CaptureMissionHtmlOnce.Value = false;
            Plugin.Logger.LogInfo("[QuestHelper] Original Missions HTML " +
                "captured once, read-only: " + icons.Count +
                " icon markers; " + html.Length +
                " characters. File: " + fullPath);
            Plugin.Logger.LogInfo("[QuestHelper] Do NOT publish captured HTML " +
                "on GitHub; share privately for offline transformer validation.");
            Plugin.Logger.LogInfo("[QuestHelper] Safe mode remains active; " +
                "no mission groups or UI elements were changed.");
        }
        catch (Exception ex)
        {
            if (_reportedError) return;
            _reportedError = true;
            Plugin.Logger.LogWarning("[QuestHelper] Read-only Missions " +
                "HTML capture unavailable: " + ex.GetType().Name +
                " " + ex.Message);
        }
    }
}

/// <summary>
/// Managed-only static helper: no Regex or other unsupported reference field
/// is exposed on the IL2CPP-injected MonoBehaviour.
/// </summary>
internal static class MissionMarkupCounter
{
    private static readonly Regex MissionMarker = new(
        @"(?:^|\s)data-missionid-([A-Za-z0-9_-]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(200));

    internal static int Count(string html) => MissionMarker.Matches(html).Count;
}

using System;
using UnityEngine;

namespace OrbusQuestHelper;

/// <summary>
/// Always-on, read-only pinned HUD updater. Mission browsing/grouping and
/// pinned objective drawing have independent lifecycles: a user should not
/// need to reopen Inventory > Missions to restart the tracker. The poll is
/// bounded in MissionHudPresenter and only reads the active player's data.
/// </summary>
public sealed class MissionHudDriver : MonoBehaviour
{
    public MissionHudDriver(IntPtr pointer) : base(pointer) { }

    private void Update()
    {
        MissionHudPresenter.Tick();
    }

    private void OnDestroy()
    {
        MissionHudPresenter.Shutdown();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using GatherBuddy.Automation;
using GatherBuddy.ForkLogic;
using GatherBuddy.Helpers;
using GatherBuddy.Plugin;
using Lumina.Excel.Sheets;
using TaskResult = GatherBuddy.Crafting.CraftingTasks.TaskResult;

namespace GatherBuddy.Crafting;

// Fork only. Repair All covers only the dropdown's current category, so the window is stepped through each worn one.
public static unsafe class AfterRunRepair
{
    private const string WaitStep = "wait for the run to end";

    private static readonly Queue<(string Name, int Seconds, Func<TaskResult> Run)> Steps = new();

    private static readonly (RepairRules.Category Category, InventoryType[] Containers)[] Containers =
    [
        (RepairRules.Category.Equipped, [InventoryType.EquippedItems]),
        (RepairRules.Category.MainOffHand, [InventoryType.ArmoryMainHand, InventoryType.ArmoryOffHand]),
        (RepairRules.Category.HeadBodyHands, [InventoryType.ArmoryHead, InventoryType.ArmoryBody, InventoryType.ArmoryHands]),
        (RepairRules.Category.LegsFeet, [InventoryType.ArmoryLegs, InventoryType.ArmoryFeets]),
        (RepairRules.Category.NeckEars, [InventoryType.ArmoryNeck, InventoryType.ArmoryEar]),
        (RepairRules.Category.WristsRings, [InventoryType.ArmoryWrist, InventoryType.ArmoryRings]),
        (RepairRules.Category.Inventory, [InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4]),
    ];

    private static string         _reason = string.Empty;
    private static string?        _label;
    private static bool           _homeWithoutTrip;
    private static bool           _clicked;
    private static bool           _homeStarted;
    private static bool           _locked;
    private static int            _gilBefore;
    private static DateTime       _stepSince;
    private static string?        _stepName;
    private static DateTime       _nextAt;
    private static RepairNPCData? _mender;
    private static string?        _waitingOn;

    public static bool Busy
        => Steps.Count > 0;

    public static bool Wanted(bool partOfCraftingRun)
    {
        var enabled = GatherBuddy.Config.VulcanRepairConfig.RepairAfterRun;
        var inDuty  = Dalamud.Conditions[ConditionFlag.BoundByDuty];
        if (RepairRules.AfterRun(enabled, partOfCraftingRun, inDuty, RunWork.Any))
            return true;

        if (RepairRules.AfterRun(enabled, partOfCraftingRun, inDuty, true))
            ForkTrace.Info("after-run repair skipped: the run gathered nothing and crafted nothing");
        return false;
    }

    public static void Start(string reason, bool homeWithoutTrip)
    {
        Stop(null);
        _reason          = reason;
        _label           = RunLabel.Current;
        _homeWithoutTrip = homeWithoutTrip;
        ForkTrace.Info($"after-run repair ({reason}): waiting for the run to let go");
        Steps.Enqueue((WaitStep, 60, WaitUntilFree));
        Steps.Enqueue(("plan", 5, Plan));
    }

    public static void Update()
    {
        if (Steps.Count == 0)
            return;

        var (name, seconds, run) = Steps.Peek();
        if (name != WaitStep && (GatherBuddy.AutoGather.Enabled || CraftingGatherBridge.IsQueueMode))
        {
            Stop("a new run started");
            return;
        }

        if (!ReferenceEquals(name, _stepName))
        {
            _stepName  = name;
            _stepSince = DateTime.Now;
        }
        else if ((DateTime.Now - _stepSince).TotalSeconds > seconds)
        {
            Stop(name == WaitStep && _waitingOn != null ? $"{_waitingOn} after {seconds} s" : $"'{name}' took longer than {seconds} s");
            return;
        }

        TaskResult result;
        try
        {
            result = run();
        }
        catch (Exception ex)
        {
            Stop($"'{name}' failed: {ex.Message}");
            return;
        }

        if (result == TaskResult.Abort)
            Stop($"'{name}' did not work");
        else if (result == TaskResult.Done && Steps.Count > 0)
            Steps.Dequeue();
    }

    private static void Stop(string? why)
    {
        if (why != null)
        {
            ForkTrace.Info($"after-run repair ({_reason}): stopped, {why}");
            Communicator.PrintError(TextRules.WithRunLabel($"[GatherBuddy] After-run repair stopped: {why} (fork).", _label));
        }

        Steps.Clear();
        CraftingTasks.StopNavigation();
        CraftingTasks.ResetRepairState();
        if (_locked)
            YesAlready.Unlock();
        _locked      = false;
        _clicked     = false;
        _homeStarted = false;
        _mender      = null;
        _stepName    = null;
    }

    private static TaskResult WaitUntilFree()
    {
        var c = Dalamud.Conditions;
        _waitingOn = GatherBuddy.AutoGather.Enabled ? "auto-gather was still on"
            : CraftingGatherBridge.IsQueueMode ? "the crafting run had not ended"
            : GatherBuddy.CollectableManager.IsRunning ? "a collectables turn-in was still going"
            : c[ConditionFlag.Crafting] || c[ConditionFlag.ExecutingCraftingAction] || c[ConditionFlag.PreparingToCraft] ? "the crafting log was still open"
            : c[ConditionFlag.Gathering] ? "a gathering node was still open"
            : c[ConditionFlag.BetweenAreas] || c[ConditionFlag.BetweenAreas51] ? "the zone change had not finished"
            : c[ConditionFlag.OccupiedInQuestEvent] ? "a dialogue was still open"
            : c[ConditionFlag.Casting] ? "a cast was still going"
            : Dalamud.Objects.LocalPlayer == null ? "the character was not loaded"
            : Lifestream.Enabled && Lifestream.IsBusy() ? "Lifestream was still busy"
            : null;
        if (_waitingOn != null)
            return TaskResult.Retry;

        if (DateTime.Now < _nextAt)
            return TaskResult.Retry;

        if (_nextAt == DateTime.MinValue)
        {
            _nextAt = DateTime.Now.AddSeconds(2);
            return TaskResult.Retry;
        }

        _nextAt = DateTime.MinValue;
        return TaskResult.Done;
    }

    private static TaskResult Plan()
    {
        var threshold = GatherBuddy.Config.VulcanRepairConfig.AfterRunThreshold;
        var worn      = RepairRules.Worn(Pieces(), threshold);
        if (worn.Count == 0)
        {
            ForkTrace.Info($"after-run repair ({_reason}): every piece is at {threshold}% or better");
            if (_homeWithoutTrip)
                Steps.Enqueue(("go home", 120, GoHome));
            return TaskResult.Done;
        }

        var known  = RepairNPCHelper.RepairNPCs;
        var choice = RepairRules.Choose(known.Select(n => new RepairRules.Mender(n.DataId, n.TerritoryType)).ToList(),
            GatherBuddy.Config.VulcanRepairConfig.PreferredRepairNPCDataId, Dalamud.ClientState.TerritoryType);
        _mender = choice is { } chosen ? known.First(n => n.DataId == chosen.Id) : null;
        if (_mender == null)
        {
            Communicator.PrintError(TextRules.WithRunLabel("[GatherBuddy] After-run repair: no mender is known; pick one under Preferred Repair NPC in Vulcan's settings (fork).", _label));
            ForkTrace.Info($"after-run repair ({_reason}): no mender known ({known.Count} listed)");
            if (_homeWithoutTrip)
                Steps.Enqueue(("go home", 120, GoHome));
            return TaskResult.Done;
        }

        ForkTrace.Info($"after-run repair ({_reason}): {string.Join(", ", worn)} below {threshold}%; "
          + $"mender {_mender.Name} ({_mender.DataId}) in territory {_mender.TerritoryType}, now in {Dalamud.ClientState.TerritoryType}");
        _gilBefore = Gil();
        YesAlready.Lock();
        _locked = true;
        CraftingTasks.ResetRepairState();
        var mender = _mender;
        Steps.Enqueue(("go to the mender", 150, () => CraftingTasks.TaskNavigateToRepairNPC(mender)));
        Steps.Enqueue(("talk to the mender", 10, CraftingTasks.TaskInteractWithRepairNPC));
        Steps.Enqueue(("open repairs", 15, CraftingTasks.TaskSelectRepairFromMenu));
        foreach (var category in worn)
        {
            Steps.Enqueue(($"show {RepairRules.Label(category)}", 10, () => Show(category)));
            Steps.Enqueue(($"repair {RepairRules.Label(category)}", 20, RepairShown));
        }

        Steps.Enqueue(("close repairs", 15, CraftingTasks.TaskCloseRepairWindow));
        Steps.Enqueue(("report", 5, Report));
        Steps.Enqueue(("let go", 5, LetGo));
        if (GatherBuddy.Config.AutoGatherConfig.GoHomeWhenDone)
            Steps.Enqueue(("go home", 120, GoHome));
        return TaskResult.Done;
    }

    private static TaskResult Show(RepairRules.Category category)
    {
        if (DateTime.Now < _nextAt)
            return TaskResult.Retry;

        if (!RepairManager.RepairWindowOpen())
            return TaskResult.Abort;

        var agent  = (AgentRepair*)AgentModule.Instance()->GetAgentByInternalId(AgentId.Repair);
        var filter = (AgentRepair.ItemFilter)(int)category;
        if (agent->Filter != filter)
        {
            agent->Filter = filter;
            agent->ChangeRepairInventory(false);
            _nextAt = DateTime.Now.AddMilliseconds(600);
            return TaskResult.Retry;
        }

        if (agent->IsAddonRefreshPending)
        {
            _nextAt = DateTime.Now.AddMilliseconds(100);
            return TaskResult.Retry;
        }

        ForkTrace.Info($"after-run repair: showing {category} ({agent->ShownRepairEntryAmount} item(s), {agent->TotalRepairCost} gil)");
        return TaskResult.Done;
    }

    private static TaskResult RepairShown()
    {
        if (DateTime.Now < _nextAt)
            return TaskResult.Retry;

        if (!_clicked)
        {
            if (!GenericHelpers.TryGetAddonByName<AddonRepair>("Repair", out var addon)
             || addon->RepairAllButton == null
             || !addon->RepairAllButton->IsEnabled)
            {
                ForkTrace.Info("after-run repair: nothing to repair in this category");
                return TaskResult.Done;
            }

            _clicked = true;
        }

        var result = CraftingTasks.TaskExecuteRepair();
        if (result == TaskResult.Retry)
            return result;

        _clicked = false;
        _nextAt  = DateTime.Now.AddSeconds(1);
        return result;
    }

    private static TaskResult Report()
    {
        var threshold = GatherBuddy.Config.VulcanRepairConfig.AfterRunThreshold;
        var still     = RepairRules.Worn(Pieces(), threshold);
        var spent     = _gilBefore - Gil();
        var where     = _mender?.Name ?? "the mender";
        ForkTrace.Info($"after-run repair ({_reason}): done at {where}, {spent} gil; still below {threshold}%: "
          + (still.Count == 0 ? "none" : string.Join(", ", still)));
        if (still.Count == 0)
            Communicator.PrintRunEnd($"[GatherBuddy] Repaired all gear after the run at {where} ({spent:N0} gil) (fork).", _label);
        else
            Communicator.PrintError(TextRules.WithRunLabel($"[GatherBuddy] Repaired gear after the run at {where} ({spent:N0} gil), but "
              + $"{string.Join(", ", still.Select(RepairRules.Label))} is still below {threshold}% (fork).", _label));
        return TaskResult.Done;
    }

    private static TaskResult GoHome()
    {
        if (!_homeStarted)
        {
            if (!HomeNavigationHelper.TryStartReturnHome(out var error, "after repair"))
            {
                if (error == null)
                    return TaskResult.Retry;

                ForkTrace.Info($"go home (after repair): {error}");
                return TaskResult.Done;
            }

            _homeStarted = true;
            _nextAt      = DateTime.Now.AddSeconds(2);
            return TaskResult.Retry;
        }

        if (DateTime.Now < _nextAt || !HomeNavigationHelper.IsReturnComplete())
            return TaskResult.Retry;

        ForkTrace.Info($"go home (after repair): Lifestream finished, now in territory {Dalamud.ClientState.TerritoryType}");
        _homeStarted = false;
        return TaskResult.Done;
    }

    private static TaskResult LetGo()
    {
        if (_locked)
            YesAlready.Unlock();
        _locked = false;
        _mender = null;
        return TaskResult.Done;
    }

    private static int Gil()
        => (int)InventoryManager.Instance()->GetInventoryItemCount(1);

    private static List<RepairRules.Piece> Pieces()
    {
        var pieces    = new List<RepairRules.Piece>();
        var items     = Dalamud.GameData.GetExcelSheet<Item>();
        var inventory = InventoryManager.Instance();
        foreach (var (category, containers) in Containers)
        {
            foreach (var type in containers)
            {
                var container = inventory->GetInventoryContainer(type);
                if (container == null)
                    continue;

                for (var i = 0; i < container->Size; ++i)
                {
                    var slot = container->GetInventorySlot(i);
                    if (slot == null || slot->ItemId == 0)
                        continue;

                    var repairable = items.TryGetRow(slot->ItemId, out var item) && item.ClassJobRepair.RowId > 0;
                    pieces.Add(new RepairRules.Piece(category, RepairRules.Percent(slot->Condition), repairable));
                }
            }
        }

        return pieces;
    }
}

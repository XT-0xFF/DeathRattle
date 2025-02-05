﻿using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace DeathRattle.Harmony;

[HarmonyPatch(typeof(Pawn_HealthTracker), "ShouldBeDeadFromRequiredCapacity")]
public static class ShouldBeDeadFromRequiredCapacityPatch
{
    public static Dictionary<string, HediffDef> ailmentDictionary = new()
    {
        { "Metabolism", HediffDefOfDeathRattle.IntestinalFailure },
        { "BloodFiltration", null },
        { "BloodPumping", HediffDefOfDeathRattle.ClinicalDeathNoHeartbeat },
        { "Breathing", HediffDefOfDeathRattle.ClinicalDeathAsphyxiation },
        { "Consciousness", HediffDefOfDeathRattle.Coma }
    };

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> DeathRattleException(IEnumerable<CodeInstruction> instrs,
        ILGenerator gen)
    {
        var trigger = false;
        foreach (var itr in instrs)
        {
            yield return itr;
            if (trigger)
            {
                trigger = false;
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Pawn_HealthTracker), "pawn"));
                yield return new CodeInstruction(OpCodes.Ldloc_2);
                yield return new CodeInstruction(OpCodes.Callvirt,
                    AccessTools.Method(typeof(ShouldBeDeadFromRequiredCapacityPatch), "AddCustomHediffs",
                        new[] { typeof(Pawn_HealthTracker), typeof(Pawn), typeof(PawnCapacityDef) }));
                yield return itr;
            }

            if (itr.opcode == OpCodes.Callvirt && itr.operand.Equals(AccessTools.Method(typeof(PawnCapacitiesHandler),
                    "CapableOf", new[] { typeof(PawnCapacityDef) }))) trigger = true;
        }
    }

    public static bool AddCustomHediffs(Pawn_HealthTracker tracker, Pawn pawn, PawnCapacityDef pawnCapacityDef)
    {
        if (pawn.health.hediffSet.GetBrain() == null) return false;
        if (pawn.RaceProps.IsFlesh && pawnCapacityDef.lethalFlesh && !tracker.capacities.CapableOf(pawnCapacityDef) &&
            ailmentDictionary.ContainsKey(pawnCapacityDef.defName))
        {
            var def = ailmentDictionary[pawnCapacityDef.defName];
            if (def == null)
            {
                if (pawn.health.hediffSet.GetNotMissingParts(depth: BodyPartDepth.Inside)
                        .FirstOrDefault(p => p.def.defName == "Liver") == null &&
                    !pawn.health.hediffSet.HasHediff(HediffDefOfDeathRattle.LiverFailure))
                    def = HediffDefOfDeathRattle.LiverFailure;
                else if (pawn.health.hediffSet.GetNotMissingParts(depth: BodyPartDepth.Inside)
                             .FirstOrDefault(p => p.def.defName.Contains("Kidney")) ==
                         null) def = HediffDefOfDeathRattle.KidneyFailure;
            }

            if (def != null && !pawn.health.hediffSet.HasHediff(def))
            {
                var ailment = HediffMaker.MakeHediff(def, pawn) as Hediff_DeathRattle;
                ailment.cause = pawnCapacityDef;
                pawn.health.AddHediff(ailment);
            }

            return true;
        }

        return false;
    }
}
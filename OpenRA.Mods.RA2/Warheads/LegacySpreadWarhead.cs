#region Copyright & License Information
/*
 * Copyright 2015- OpenRA.Mods.AS Developers (see AUTHORS)
 * This file is a part of a third-party plugin for OpenRA, which is
 * free software. It is made available to you under the terms of the
 * GNU General Public License as published by the Free Software
 * Foundation. For more information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using OpenRA.GameRules;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Traits;

namespace OpenRA.Mods.RA2.Warheads
{
	[Desc("Warhead used to simulate the Red Alert 2 CellSpread damage delivery model.")]
	public class LegacySpreadWarhead : DamageWarhead
	{
		[Desc("Damage will be applied to actors in this area. A value of zero means only targeted actor will be damaged.")]
		public readonly WDist Spread = WDist.Zero;

		[Desc("The minimum percentage delivered at the edge of the spread.")]
		public readonly int PercentAtMax = 0;

		[Desc("In vanilia RA2, each cell of a structure were affected independently. Ares offered this control instead.")]
		public readonly int MaxAffect = int.MaxValue;

		protected override void DoImpact(WPos pos, Actor firedBy, WarheadArgs args)
		{
			if (Spread == WDist.Zero)
				return;

			var debugVis = firedBy.World.WorldActor.TraitOrDefault<DebugVisualizations>();
			if (debugVis != null && debugVis.CombatGeometry)
				firedBy.World.WorldActor.Trait<WarheadDebugOverlay>().AddImpact(pos, new[] { WDist.Zero, Spread }, DebugOverlayColor);

			foreach (var victim in firedBy.World.FindActorsOnCircle(pos, Spread))
			{
				if (!IsValidAgainst(victim, firedBy))
					continue;

				// PERF: Find closest active HitShape using manual loop instead of LINQ
				// Original: victim.TraitsImplementing<HitShape>().Where(Exts.IsTraitEnabled)
				//           .Select(s => (s, s.DistanceFromEdge(victim, pos))).MinByOrDefault(s => s.Item2);
				HitShape closestShape = null;
				var closestDistance = WDist.MaxValue;
				foreach (var shape in victim.TraitsImplementing<HitShape>())
				{
					if (!shape.IsTraitEnabled())
						continue;

					var distance = shape.DistanceFromEdge(victim, pos);
					if (distance < closestDistance)
					{
						closestShape = shape;
						closestDistance = distance;
					}
				}

				// Cannot be damaged without an active HitShape or if HitShape is outside Spread
				if (closestShape == null || closestDistance > Spread)
					continue;

				var building = victim.TraitOrDefault<Building>();

				// PERF: Build modifiers list manually instead of using Append (avoids allocations)
				var baseDamageVersus = DamageVersus(victim, closestShape, args);
				var modifiersList = new List<int>(args.DamageModifiers.Length + 2);
				modifiersList.AddRange(args.DamageModifiers);
				modifiersList.Add(baseDamageVersus);

				if (MaxAffect > 0 && building != null)
				{
					// PERF: Manual min-finding instead of OrderBy().Take()
					// NOTE: Original logic uses "x > Spread.Length" which may be inverted (kept as-is for correctness)
					var occupiedCells = building.OccupiedCells();
					var cellDistances = new List<int>(occupiedCells.Length);
					foreach (var cell in occupiedCells)
					{
						var cellDist = (pos - firedBy.World.Map.CenterOfCell(cell.Item1)).Length;
						if (cellDist > Spread.Length)
							cellDistances.Add(cellDist);
					}

					// Sort to get the closest N cells (up to MaxAffect)
					cellDistances.Sort();
					var affectCount = cellDistances.Count < MaxAffect ? cellDistances.Count : MaxAffect;

					var delivereddamage = 0;
					for (var i = 0; i < affectCount; i++)
					{
						// Create a copy for this specific damage calculation
						var cellModifiers = new List<int>(modifiersList)
						{
							int2.Lerp(PercentAtMax, 100, cellDistances[i], Spread.Length)
						};
						delivereddamage += Util.ApplyPercentageModifiers(Damage, cellModifiers);
					}

					victim.InflictDamage(firedBy, new Damage(delivereddamage, DamageTypes));
				}
				else
				{
					modifiersList.Add(int2.Lerp(PercentAtMax, 100, closestDistance.Length, Spread.Length));
					var damage = Util.ApplyPercentageModifiers(Damage, modifiersList);
					victim.InflictDamage(firedBy, new Damage(damage, DamageTypes));
				}
			}
		}
	}
}

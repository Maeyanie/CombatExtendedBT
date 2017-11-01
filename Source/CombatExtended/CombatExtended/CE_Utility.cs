﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;
using UnityEngine;

namespace CombatExtended
{
    static class CE_Utility
    {
    	
    	#region Blitting
		/// <summary>
		/// Code from https://gamedev.stackexchange.com/questions/92285/unity3d-resize-texture-without-corruption
		/// </summary>
		/// <param name="texture"></param>
		/// <returns></returns>
    	public static Color[] Blit(this Texture2D texture)
		{
			Color[] color = null;
			try
			{
				color = texture.GetPixels();
			}
			catch
			{
				var prevFilterMode = texture.filterMode;
				texture.filterMode = FilterMode.Point;
															   //prev width,    prev height, no depth buffer, default color mode,          default r/w mode, 1 (= none) anti-aliasing
				RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default, 1);
				rt.filterMode = FilterMode.Point;
				RenderTexture.active = rt;
				Graphics.Blit(texture, rt);
				
				Texture2D blit = new Texture2D(texture.width, texture.height);
				blit.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
				blit.Apply();
				
				RenderTexture.active = null;
				texture.filterMode = prevFilterMode;
				
				color = blit.GetPixels();
			}
			return color;
		}
    	
    	public static Texture2D BlitCrop(this Texture2D texture, IntRange horz, IntRange vert)
		{
			var prevFilterMode = texture.filterMode;
			texture.filterMode = FilterMode.Point;
														   //prev width,    prev height, no depth buffer, default color mode,          default r/w mode, 1 (= none) anti-aliasing
			RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default, 1);
			rt.filterMode = FilterMode.Point;
			RenderTexture.active = rt;
			Graphics.Blit(texture, rt);
			
			Texture2D blit = new Texture2D(horz.max - horz.min, vert.max - vert.min);
			blit.ReadPixels(new Rect(horz.min, vert.min, horz.max - horz.min, vert.max - vert.min), 0, 0);
			blit.Apply();
			
			RenderTexture.active = null;
			texture.filterMode = prevFilterMode;
			
			return blit;
		}
    	#endregion
    	
        #region Misc
        public static List<ThingDef> allWeaponDefs = new List<ThingDef>();

        /// <summary>
        /// Generates a random Vector2 in a circle with given radius
        /// </summary>
        public static Vector2 GenRandInCircle(float radius)
        {
            //Fancy math to get random point in circle
            System.Random rand = new System.Random();
            double angle = rand.NextDouble() * Math.PI * 2;
            double range = Math.Sqrt(rand.NextDouble()) * radius;
            return new Vector2((float)(range * Math.Cos(angle)), (float)(range * Math.Sin(angle)));
        }

        /// <summary>
        /// Calculates the actual current movement speed of a pawn
        /// </summary>
        /// <param name="pawn">Pawn to calculate speed of</param>
        /// <returns>Move speed in cells per second</returns>
        public static float GetMoveSpeed(Pawn pawn)
        {
            float movePerTick = 60 / pawn.GetStatValue(StatDefOf.MoveSpeed, false);    //Movement per tick
            movePerTick +=  pawn.Map.pathGrid.CalculatedCostAt(pawn.Position, false, pawn.Position);
            Building edifice = pawn.Position.GetEdifice(pawn.Map);
            if (edifice != null)
            {
                movePerTick += (int)edifice.PathWalkCostFor(pawn);
            }

            //Case switch to handle walking, jogging, etc.
            if (pawn.CurJob != null)
            {
                switch (pawn.CurJob.locomotionUrgency)
                {
                    case LocomotionUrgency.Amble:
                        movePerTick *= 3;
                        if (movePerTick < 60)
                        {
                            movePerTick = 60;
                        }
                        break;
                    case LocomotionUrgency.Walk:
                        movePerTick *= 2;
                        if (movePerTick < 50)
                        {
                            movePerTick = 50;
                        }
                        break;
                    case LocomotionUrgency.Jog:
                        break;
                    case LocomotionUrgency.Sprint:
                        movePerTick = Mathf.RoundToInt(movePerTick * 0.75f);
                        break;
                }
            }
            return 60 / movePerTick;
        }
        
        public static float ClosestDistBetween(Vector2 origin, Vector2 destination, Vector2 target)
        {
        	return Mathf.Abs((destination.y - origin.y) * target.x - (destination.x - origin.x) * target.y + destination.x * origin.y - destination.y * origin.x) / (destination - origin).magnitude;
        }

        /// <summary>
        /// Attempts to find a turret operator. Accepts any Thing as input and does a sanity check to make sure it is an actual turret.
        /// </summary>
        /// <param name="thing">The turret to check for an operator</param>
        /// <returns>Turret operator if one is found, null if not</returns>
        public static Pawn TryGetTurretOperator(Thing thing)
        {
            Pawn manningPawn = null;
            Building_TurretGun turret = thing as Building_TurretGun;
            if (turret != null)
            {
                CompMannable comp = turret.TryGetComp<CompMannable>();
                if (comp != null && comp.MannedNow)
                {
                    manningPawn = comp.ManningPawn;
                }
            }
            return manningPawn;
        }

        /// <summary>
        /// Extension method to determine whether a ranged weapon has ammo available to it
        /// </summary>
        /// <returns>True if the gun has no CompAmmoUser, doesn't use ammo or has ammo in its magazine or carrier inventory, false otherwise</returns>
        public static bool HasAmmo(this ThingWithComps gun)
        {
            CompAmmoUser comp = gun.TryGetComp<CompAmmoUser>();
            if (comp == null) return true;
            return !comp.UseAmmo || comp.CurMagCount > 0 || comp.HasAmmo;
        }

        public static bool CanBeStabilized(this Hediff diff)
        {
            HediffWithComps hediff = diff as HediffWithComps;
            if (hediff == null)
            {
                return false;
            }
            if (hediff.BleedRate == 0f || hediff.IsTended() || hediff.IsOld())
            {
                return false;
            }
            HediffComp_Stabilize comp = hediff.TryGetComp<HediffComp_Stabilize>();
            return comp != null && !comp.Stabilized;
        }

        #endregion Misc

        #region MoteThrower
        public static void ThrowEmptyCasing(Vector3 loc, Map map, ThingDef casingMoteDef, float size = 1f)
        {
            if (!Controller.settings.ShowCasings || !loc.ShouldSpawnMotesAt(map) || map.moteCounter.SaturatedLowPriority)
            {
                return;
            }
            MoteThrown moteThrown = (MoteThrown)ThingMaker.MakeThing(casingMoteDef, null);
            moteThrown.Scale = Rand.Range(0.5f, 0.3f) * size;
            moteThrown.exactRotation = Rand.Range(-3f, 4f);
            moteThrown.exactPosition = loc;
            moteThrown.airTimeLeft = 60;
            moteThrown.SetVelocity((float)Rand.Range(160, 200), Rand.Range(0.7f, 0.5f));
            //     moteThrown.SetVelocityAngleSpeed((float)Rand.Range(160, 200), Rand.Range(0.020f, 0.0115f));
            GenSpawn.Spawn(moteThrown, loc.ToIntVec3(), map);
        }
        #endregion

        #region Physics
        /// <summary>
        /// Gravity constant in meters per second squared
        /// </summary>
        public const float gravityConst = 9.8f;
		
        public static Bounds GetBoundsFor(IntVec3 cell, RoofDef roof)
        {
        	if (roof == null)
        		return new Bounds();
        	
        	float height = CollisionVertical.WallCollisionHeight;
        	
        	if (roof.isNatural)
        		height *= CollisionVertical.NaturalRoofThicknessMultiplier;
        	
        	if (roof.isThickRoof)
        		height *= CollisionVertical.ThickRoofThicknessMultiplier;
        	
        	height = Mathf.Max(0.1f, height - CollisionVertical.WallCollisionHeight);
        	
        	Vector3 center = cell.ToVector3Shifted();
        	center.y = CollisionVertical.WallCollisionHeight + height / 2f;
        	
        	return new Bounds(center,
        	                  new Vector3(1f, height, 1f));
        }
        
        public static Bounds GetBoundsFor(Thing thing)
        {
            if (thing == null)
            {
                return new Bounds();
            }
            var height = new CollisionVertical(thing);
            var width = GetCollisionWidth(thing);
            var thingPos = thing.DrawPos;
            thingPos.y = height.Max - height.HeightRange.Span / 2;
            Bounds bounds = new Bounds(thingPos, new Vector3(width, height.HeightRange.Span, width));
            return bounds;
        }

        /// <summary>
        /// Calculates the width of an object for purposes of bullet collision. Return value is distance from center of object to its edge in cells, so a wall filling out an entire cell has a width of 0.5.
        /// Also accounts for general body type, humanoids must be specified in the humanoidBodyList and will have reduced width relative to their overall body size.
        /// </summary>
        /// <param name="thing">The Thing to measure width of</param>
        /// <returns>Distance from center of Thing to its edge in cells</returns>
        public static float GetCollisionWidth(Thing thing)
        {
        	/* Possible solution for fixing tree widths
			if (thing.IsTree())
        	{
        		return (thing as Plant).def.graphicData.shadowData.volume.x;
        	}*/
        	
            var pawn = thing as Pawn;
            if (pawn != null)
            {
            	return pawn.RaceProps.Humanlike
            		? pawn.BodySize * GetCollisionBodyFactors(pawn).First * 2f
            		: BoundsInjector.ForPawn(pawn).First;
            }
            
            return 1f;    //Buildings, etc. fill out a full square
        }

        /// <summary>
        /// Calculates body scale factors based on body type
        /// </summary>
        /// <param name="pawn">Which pawn to measure for</param>
        /// <returns>Width factor as First, height factor as second</returns>
        public static Pair<float, float> GetCollisionBodyFactors(Pawn pawn)
        {
            if (pawn == null)
            {
                Log.Error("CE calling GetCollisionBodyHeightFactor with nullPawn");
                return new Pair<float, float>(1, 1);
            }
            RacePropertiesExtensionCE props = pawn.def.GetModExtension<RacePropertiesExtensionCE>() ?? new RacePropertiesExtensionCE();
            var shape = props.bodyShape;
            if (shape == CE_BodyShapeDefOf.Invalid) Log.ErrorOnce("CE returning BodyType Undefined for pawn " + pawn.ToString(),  35000198 + pawn.GetHashCode());
            if (pawn.GetPosture() != PawnPosture.Standing)
            {
                return new Pair<float, float>(shape.widthLaying, shape.heightLaying);
            }
            return new Pair<float, float>(shape.width, shape.height);
        }

        /// <summary>
        /// Determines whether a pawn should be currently crouching down or not
        /// </summary>
        /// <returns>True for humanlike pawns currently doing a job during which they should be crouching down</returns>
        public static bool IsCrouching(this Pawn pawn)
        {
            return pawn.RaceProps.Humanlike && (pawn.CurJob?.def.GetModExtension<JobDefExtensionCE>()?.isCrouchJob ?? false);
        }

        public static bool IsTree(this Thing thing)
        {
            return thing.def.category == ThingCategory.Plant && thing.def.altitudeLayer == AltitudeLayer.Building;
        }

        #endregion Physics

        #region Inventory

        public static void TryUpdateInventory(Pawn pawn)
        {
            if (pawn != null)
            {
                CompInventory comp = pawn.TryGetComp<CompInventory>();
                if (comp != null)
                {
                    comp.UpdateInventory();
                }
            }
        }

        public static void TryUpdateInventory(ThingOwner owner)
        {
            Pawn pawn = owner?.Owner?.ParentHolder as Pawn;
            if (pawn != null)
            {
                TryUpdateInventory(pawn);
            }
        }

        #endregion
    }
}
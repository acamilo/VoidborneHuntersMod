using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using System.Linq;
using VRageMath; // Required for Vector3D
using System.IO;


namespace acamilo.voidbornehunters
{

    public class Signature {
        public double magnitude;
        public long entity_id;
        public long owner_id;
        public Vector3D location;
        public long expiryTick;
        private const int lifetime = 3600;
        public bool detectableBy(Vector3D sensor,double sensitivity){
            double distance = (sensor - this.location).Length();
            double detected_energy = this.magnitude / Math.Pow(distance,2.0);
            if (detected_energy>sensitivity)
                return true;
            return false;

        }
        public Signature(double magnitude, long entity_id, long owner_id, Vector3D location)
        {
            this.magnitude = magnitude;
            this.entity_id = entity_id;
            this.location = location;
            this.owner_id = owner_id;
            this.expiryTick = MyAPIGateway.Session.GameplayFrameCounter+lifetime+new Random().Next(0, 300);
        }
        public override string ToString()
        {
            return $"Signature(EntityID: {entity_id}, Magnitude: {magnitude}, " +
                $"Location: {location}, Expire on Tick: {expiryTick})";
        }

    }
    // This shows how to find all grids and execute code on them as if it was a gamelogic component.
    // This is needed because attaching gamelogic to grids does not work reliably, like not working at all for clients in MP.
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class GridEvaluator_GridLogicSession : MySessionComponentBase
    {
        private const int search_interval = 300; // 5 seconds in game ticks (60 ticks per second)
        private int tickCounter = 0;
        private readonly Dictionary<long, IMyCubeGrid> grids = new Dictionary<long, IMyCubeGrid>();
        public readonly Dictionary<long,Signature> signatures = new Dictionary<long,Signature>();

        public override void LoadData()
        {
            MyAPIGateway.Entities.OnEntityAdd += EntityAdded;
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Entities.OnEntityAdd -= EntityAdded;

            grids.Clear();
        }

        private void EntityAdded(IMyEntity ent)
        {
            var grid = ent as IMyCubeGrid;

            if(grid != null)
            {
                grids.Add(grid.EntityId, grid);
                grid.OnMarkForClose += GridMarkedForClose;
            }
        }

        private void GridMarkedForClose(IMyEntity ent)
        {
            grids.Remove(ent.EntityId);
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                // Run every search_interval
                tickCounter++;
                if (tickCounter <= search_interval)
                        return;
                tickCounter=0;

                // Remove stale signatures
                
                foreach(var kv in signatures.ToList()){
                    Signature s = kv.Value;
                    if (MyAPIGateway.Session.GameplayFrameCounter>s.expiryTick)
                        signatures.Remove(s.entity_id);
                        //MyLog.Default.WriteLineAndConsole($"Removing Stale Signature {s}");
                }
                MyLog.Default.WriteLineAndConsole($"Total Signatures: {signatures.Count}");

                // Generate signature for each grid
                foreach(var grid in grids.Values)
                {
                    if(grid.MarkedForClose)
                        continue;
                    
                    Signature sig = calculateSignature(grid);
                    
                    if (!signatures.ContainsKey(grid.EntityId))
                    {
                        
                        signatures.Add(grid.EntityId, sig);
                        //MyLog.Default.WriteLineAndConsole($"Adding New Signature {sig}");
                    } else {
                        signatures[grid.EntityId]=sig;
                    }
                }
            }
            catch(Exception e)
            {
                MyLog.Default.WriteLineAndConsole($"{e.Message}\n{e.StackTrace}");

                if(MyAPIGateway.Session?.Player != null)
                    MyAPIGateway.Utilities.ShowNotification($"[ ERROR: {GetType().FullName}: {e.Message} | Send SpaceEngineers.Log to mod author ]", 10000, MyFontEnum.Red);
            }
        }

        private const double speed_weight=0.2;
        private const double mass_weight_exp=1.3;
        private const double mass_weight_scale=0.4;
        private const double energy_weight=0.5;
        private const double jumping_amplification_factor = 30;
        public static Signature calculateSignature(IMyCubeGrid grid){
            // Collect grid info
            float grid_power  = GetGridPowerConsumption(grid);

            double mass = 0;
            double speed = 0;
            if (grid.Physics != null)
            {
                mass = grid.Physics.Mass;
                speed = grid.Physics.LinearVelocity.Length();
                
            }

            bool is_jumping = IsAnyJumpDriveJumping(grid);
            
            double speed_q = speed* speed_weight;
            double mass_q = mass * (Math.Pow(mass,mass_weight_exp)*mass_weight_scale)/mass;
            double energy_q = grid_power*energy_weight;

            double sig_magnitude = speed_q + mass_q + energy_q;
            sig_magnitude = sig_magnitude*(is_jumping ? jumping_amplification_factor : 1.0);
            //MyLog.Default.WriteLineAndConsole($"Name: {grid.DisplayName}\tMass: {mass}\tSpeed: {speed}\tEnergy: {grid_power}");
            
            long ownerId = (grid.BigOwners.Count > 0) ? grid.BigOwners[0] : 0;
            return new Signature(sig_magnitude,grid.EntityId,ownerId,grid.WorldMatrix.Translation);
        }
        public static bool IsAnyJumpDriveJumping(IMyCubeGrid grid)
        {
            List<IMyJumpDrive> jumpDrives = GetJumpDrives(grid);

            foreach (IMyJumpDrive jumpDrive in jumpDrives)
            {
                if (jumpDrive.Status.ToString() == "Jumping")
                {
                    return true; // At least one Jump Drive is jumping
                }
            }

            return false; // No Jump Drives are currently jumping
        }

        
        public static List<IMyJumpDrive> GetJumpDrives(IMyCubeGrid grid)
        {
            List<IMySlimBlock> blocks = new List<IMySlimBlock>();
            grid.GetBlocks(blocks);

            List<IMyJumpDrive> jumpDrives = new List<IMyJumpDrive>();

            foreach (var block in blocks)
            {
                IMyJumpDrive jumpDrive = block.FatBlock as IMyJumpDrive; // Explicit cast
                if (jumpDrive != null) // Check if the cast succeeded
                {
                    jumpDrives.Add(jumpDrive);
                }
            }

            return jumpDrives;
        }
        public static float GetGridPowerConsumption(IMyCubeGrid grid)
        {
            List<IMySlimBlock> blocks = new List<IMySlimBlock>();
            grid.GetBlocks(blocks);

            float totalPowerUsage = 0f;

            foreach (IMySlimBlock block in blocks)
            {
                IMyPowerProducer powerBlock = block.FatBlock as IMyPowerProducer;
                if (powerBlock != null && powerBlock.Enabled) // Only count enabled power sources
                {
                    totalPowerUsage += powerBlock.CurrentOutput; // Current power output in MW
                }
            }

            return totalPowerUsage;
        }
    }
}


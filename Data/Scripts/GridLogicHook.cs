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


    public class GridUtilities {
        public static bool IsSuitAntennaOn(IMyPlayer player)
        {
            return true; // don't know how to do this
        }
        public static bool IsPlayerNearPosition(IMyPlayer player, Vector3D position,double threshold)
        {
            if (player == null)
                return false;

            Vector3D playerPosition = GetPlayerPosition(player);

            double distance = Vector3D.Distance(playerPosition, position);

            return distance <= threshold; // True if player is within 1km
        }
        public static Vector3D GetPlayerPosition(IMyPlayer player)
        {
            if (player.Character != null) // If the player is on foot
            {
                return player.Character.WorldMatrix.Translation;
            }
            else if (player.Controller?.ControlledEntity != null) // If the player is in a ship or cockpit
            {
                return player.Controller.ControlledEntity.Entity.WorldMatrix.Translation;
            }

            return Vector3D.Zero; // Default if no valid position found
        }
        public static string generateFluffDescription(bool jump, string sig, double mass,double speed,double energy,double magnitude){
            string speed_class="";
            string mass_class=";";
            string action="MONITOR";
            if (speed<20.0)
                speed_class = "GREEN";
            else if (speed<45.0)
                speed_class = "AMBER";
            else if (speed<70.0)
                speed_class = "ORANGE";
            else
                speed_class = "VERMILION";

            if (mass<10000.0)
                mass_class = "SMALL";
            else if (mass<100000.0)
                mass_class = "MEDIUM";
            else if (mass<1000000.0)
                mass_class = "LARGE";
            else if (mass<10000000.0)
                mass_class = "HEFTY";
            else if (mass<100000000.0)
                mass_class = "CHONKER";
            else
                mass_class = "OHLORDHECOMIN'";

            if (jump || speed>90.0 || energy>5.0)
                action="INTERCEPT";

            string s = $"BEGIN REPORT\n";
            s += jump?"ALERT: QUANTUM DISPLACEMENT DETECTION EVENT\nJUMP SIGNATURE DETECTED!\n\n":"ALERT: QUANTUM SUBHARMONIC DETECTION EVENT\n\n";
            s += $"SIGNATURE ID:    [{sig}]\n";
            s += $"MASS:            {mass} ({mass_class})\n";
            s += $"VELOCITY:        {speed} ({speed_class})\n";
            s += $"DETECTED ENERGY: {energy}\n";
            s += $"\n";
            s += $"TOTAL EMISSIONS: {magnitude}\n";
            s += $"RECOMMENDED ACTION: {action}";

            return s;
        }
        public static string GenerateSignatureID(long input)
        {
            // Generate a simple hash using bitwise operations
            uint hash = (uint)(input ^ (input >> 16) ^ (input >> 32));

            // Convert to hexadecimal and format with prefix
            return $"{hash:X8}"; // 8-character hex ID
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
        public static bool DoesPlayerHaveAnyOwnership(IMyCubeGrid grid, long playerId)
        {
            List<IMySlimBlock> blocks = new List<IMySlimBlock>();
            grid.GetBlocks(blocks);

            foreach (IMySlimBlock block in blocks)
            {
                IMyCubeBlock fatBlock = block.FatBlock as IMyCubeBlock;
                if (fatBlock != null && fatBlock.OwnerId == playerId)
                {
                    return true; // Player owns at least one block
                }
            }

            return false; // Player owns nothing on this grid
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
        public static double GetPlayerDetectionThreshold(IMyPlayer player)
        {

            if (player.Controller?.ControlledEntity != null)
            {
                IMyEntity entity = player.Controller.ControlledEntity.Entity;
                IMyCubeGrid shipGrid = entity.GetTopMostParent() as IMyCubeGrid;
                
                if (shipGrid != null)
                {
                    MyLog.Default.WriteLineAndConsole("Player is in a ship");
                    double threshold=0;
                    List<double> ranges = GridUtilities.GetAntennaRanges(shipGrid);
                    double contribution_quotent=1;
                    foreach (double range in ranges){
                        threshold+=range*contribution_quotent;
                        contribution_quotent=contribution_quotent*0.99;
                    }
                    MyLog.Default.WriteLineAndConsole($"Player {player} Is in a ship with {ranges.Count} antennas and a combined antenna range of {threshold}");
                    return threshold;
                }
            }  else if (player.Character != null) // Player is on foot
            {
                MyLog.Default.WriteLineAndConsole("Player is in a suit");
                return 0.01;
            }

            return 0.001; // Default fallback if undetermined
        }
        public static double GetAntennaRange(IMyCubeGrid grid)
        {
            List<IMyRadioAntenna> antennas = new List<IMyRadioAntenna>();
            MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid).GetBlocksOfType(antennas);

            double maxRange = 0;
            foreach (IMyRadioAntenna antenna in antennas)
            {
                if (antenna.IsFunctional && antenna.Enabled)
                {
                    maxRange = Math.Max(maxRange, antenna.Radius);
                }
            }

            return maxRange > 0 ? maxRange : 0.001;
        }
        public static List<double> GetAntennaRanges(IMyCubeGrid grid)
        {
            List<IMyRadioAntenna> antennas = new List<IMyRadioAntenna>();
            MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid).GetBlocksOfType(antennas);

            List<double> ranges = new List<double>();

            foreach (IMyRadioAntenna antenna in antennas)
            {
                if (antenna.IsFunctional && antenna.Enabled) // Only count working antennas
                {
                    ranges.Add(antenna.Radius); // Add the antenna's range
                }
            }

            return ranges;
        }
        
    }
    public class Signature {
        public double magnitude;
        public long entity_id;
        public long owner_id;
        public IMyGps gps_marker;
        public List<IMyPlayer> gps_marker_players = new List<IMyPlayer>();

        public Vector3D location;
        public double CalculateDetectedEnergy(Vector3D sensor){
            double distance = (sensor - this.location).Length();
            double detected_energy = this.magnitude / Math.Pow(distance,2.0);
            return detected_energy;
        }
        public bool detectableBy(Vector3D sensor,double sensitivity){

            if (CalculateDetectedEnergy(sensor)>sensitivity)
                return true;
            return false;

        }
        public Signature(IMyCubeGrid grid)
        {
            // Collect grid info
            float grid_power  = GridUtilities.GetGridPowerConsumption(grid);

            double mass = 0;
            double speed = 0;
            if (grid.Physics != null)
            {
                mass = grid.Physics.Mass;
                speed = grid.Physics.LinearVelocity.Length();
                
            }

            bool is_jumping = GridUtilities.IsAnyJumpDriveJumping(grid);
            
            double speed_q = speed* speed_weight;
            double mass_q = mass * (Math.Pow(mass,mass_weight_exp)*mass_weight_scale)/mass;
            double energy_q = grid_power*energy_weight;

            double sig_magnitude = speed_q + mass_q + energy_q;
            sig_magnitude = sig_magnitude*(is_jumping ? jumping_amplification_factor : 1.0);
            sig_magnitude = sig_magnitude * (grid_power>1.0?grid_power:1.0);
            //MyLog.Default.WriteLineAndConsole($"Name: {grid.DisplayName}\tMass: {mass}\tSpeed: {speed}\tEnergy: {grid_power}");
            
            long ownerId = (grid.BigOwners.Count > 0) ? grid.BigOwners[0] : 0;

            this.magnitude = sig_magnitude;
            this.entity_id = grid.EntityId;
            this.location = grid.WorldMatrix.Translation;
            this.owner_id = ownerId;

            string sig = GridUtilities.GenerateSignatureID(this.entity_id);
            string prefix = "M-";
            if (is_jumping) 
                prefix = "J-";
            else if (speed>0.1)
                prefix = "V-";
            sig = prefix+sig;
            this.gps_marker =  MyAPIGateway.Session.GPS.Create(
                $"[{sig}]",          // Name
                GridUtilities.generateFluffDescription(is_jumping,sig,mass,speed,grid_power,magnitude), // Description
                this.location,    // Coordinates
                true,                   // Show on HUD
                false                   // Not persistent (won't live in the GPS log)
            );
            this.gps_marker.GPSColor = new Color(255, 141, 161);
        }

        private const double speed_weight=0.2;
        private const double mass_weight_exp=1.3;
        private const double mass_weight_scale=0.4;
        private const double energy_weight=0.5;
        private const double jumping_amplification_factor = 30;
        public override string ToString()
        {
            return $"Signature(EntityID: {entity_id}, Magnitude: {magnitude}, " +
                $"Location: {location}";
        }

        public void ShowMarkerToPlayer(bool visible,IMyPlayer player){
            if (visible){
                MyAPIGateway.Session.GPS.AddGps(player.IdentityId, this.gps_marker);
                this.gps_marker.DiscardAt = MyAPIGateway.Session.ElapsedPlayTime+TimeSpan.FromSeconds(600);
                this.gps_marker_players.Add(player);
            } else {
                MyAPIGateway.Session.GPS.RemoveGps(player.IdentityId, this.gps_marker);
            }
        }
        public void RemoveMarkerFromAllPlayerHUDs(){
            foreach(IMyPlayer p in this.gps_marker_players){
//                MyLog.Default.WriteLineAndConsole($"Removing {this} from player {p}'s HUD");
                ShowMarkerToPlayer(false,p);
            }
            
        }

    }
    // This shows how to find all grids and execute code on them as if it was a gamelogic component.
    // This is needed because attaching gamelogic to grids does not work reliably, like not working at all for clients in MP.
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class GridEvaluator_GridLogicSession : MySessionComponentBase
    {
        private const int search_interval = 600; // 5 seconds in game ticks (60 ticks per second)
        private const int hud_update_interval = 18000;
        private const int no_detection_radius = 3000;
        private int search_counter = search_interval;
        private int hud_update_counter=hud_update_interval;
        private readonly Dictionary<long, IMyCubeGrid> grids = new Dictionary<long, IMyCubeGrid>();
        public readonly Dictionary<long,Signature> signatures = new Dictionary<long,Signature>();

        public readonly Dictionary<IMyGps,long> gps_expiry = new Dictionary<IMyGps, long>();

        public override void LoadData()
        {
            try {
                if (!MyAPIGateway.Multiplayer.IsServer)
                    return;
                MyAPIGateway.Entities.OnEntityAdd += EntityAdded;
            }            catch(Exception e)
            {
                MyLog.Default.WriteLineAndConsole($"{e.Message}\n{e.StackTrace}");

                if(MyAPIGateway.Session?.Player != null)
                    MyAPIGateway.Utilities.ShowNotification($"[ ERROR: {GetType().FullName}: {e.Message} | Send SpaceEngineers.Log to mod author ]", 10000, MyFontEnum.Red);
            }

        }

        protected override void UnloadData()
        {
            try {
                if (!MyAPIGateway.Multiplayer.IsServer)
                    return;
                MyAPIGateway.Entities.OnEntityAdd -= EntityAdded;

                grids.Clear();
            } catch(Exception e)
            {
                MyLog.Default.WriteLineAndConsole($"{e.Message}\n{e.StackTrace}");

                if(MyAPIGateway.Session?.Player != null)
                    MyAPIGateway.Utilities.ShowNotification($"[ ERROR: {GetType().FullName}: {e.Message} | Send SpaceEngineers.Log to mod author ]", 10000, MyFontEnum.Red);
            }

        }

        private void EntityAdded(IMyEntity ent)
        {
            try {
                if (!MyAPIGateway.Multiplayer.IsServer)
                    return;
                var grid = ent as IMyCubeGrid;

                if(grid != null)
                {
                    grids.Add(grid.EntityId, grid);
                    grid.OnMarkForClose += GridMarkedForClose;
                }
            } catch(Exception e)
            {
                MyLog.Default.WriteLineAndConsole($"{e.Message}\n{e.StackTrace}");

                if(MyAPIGateway.Session?.Player != null)
                    MyAPIGateway.Utilities.ShowNotification($"[ ERROR: {GetType().FullName}: {e.Message} | Send SpaceEngineers.Log to mod author ]", 10000, MyFontEnum.Red);
            }

        }

        private void GridMarkedForClose(IMyEntity ent)
        {
            try {
                           if (!MyAPIGateway.Multiplayer.IsServer)
                return;
            grids.Remove(ent.EntityId); 
            }catch(Exception e)
            {
                MyLog.Default.WriteLineAndConsole($"{e.Message}\n{e.StackTrace}");

                if(MyAPIGateway.Session?.Player != null)
                    MyAPIGateway.Utilities.ShowNotification($"[ ERROR: {GetType().FullName}: {e.Message} | Send SpaceEngineers.Log to mod author ]", 10000, MyFontEnum.Red);
            }

        }

        public void DoGridSearch(){
                MyLog.Default.WriteLineAndConsole($"GRID SEARCH");
                search_counter=0;

                // Generate signature for each grid
                foreach(var grid in grids.Values)
                {
                    if(grid.MarkedForClose)
                        continue;

                    Signature sig = new Signature(grid);
                    
                    if (!signatures.ContainsKey(grid.EntityId))
                    {
                        
                        signatures.Add(grid.EntityId, sig);
                    } else {
                        if (signatures[grid.EntityId].magnitude<sig.magnitude)
                            continue; // Don't overwrite a stringer signature
                        signatures[grid.EntityId]=sig;
                    }
                }
                MyLog.Default.WriteLineAndConsole($"Total Signatures: {signatures.Count}");

        }

        public void DoHudUpdate(){
            hud_update_counter=0;
            MyLog.Default.WriteLineAndConsole($"HUD UPDATE");
            // Remove stale signatures
            foreach(var kv in signatures.ToList()){
                Signature s = kv.Value;
                s.RemoveMarkerFromAllPlayerHUDs();
            }

            // Determine which players can see what signature
            // Iterate over each connected player.

            // Build a list of players
            List<IMyPlayer> players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players, null);

            // Calculate detection signatures for players
            Dictionary<long,double> player_thresholds = new Dictionary<long,double>();
            foreach (IMyPlayer player in players){
                double threshold = 1/GridUtilities.GetPlayerDetectionThreshold(player);
                threshold = threshold*10;
                MyLog.Default.WriteLineAndConsole($"Player {player} has threshold of {threshold}");
                player_thresholds.Add(player.IdentityId,threshold);
            }

            foreach (IMyPlayer player in players)
            {
                if (player?.Character == null)
                    continue;
                Vector3D player_position = GridUtilities.GetPlayerPosition(player);
                foreach(var kv in signatures.ToList()){
                    Signature s = kv.Value;
                    
                    //MyLog.Default.WriteLineAndConsole($"Energy: {s.CalculateDetectedEnergy(player_position)} Marker:{s.gps_marker}");
                    if (GridUtilities.IsPlayerNearPosition(player,s.location,no_detection_radius)){
                        //MyLog.Default.WriteLineAndConsole($"Player {player} is too close to {s} to detect it");
                        continue;
                    }
                    if (s.detectableBy(player_position,player_thresholds[player.IdentityId]))
                        s.ShowMarkerToPlayer(true,player);
                }
            }





            foreach(var kv in signatures.ToList()){
                Signature s = kv.Value;
                signatures.Remove(s.entity_id);
            }
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                // If not running on the server (i.e., running on a client), exit the method.
                if (!MyAPIGateway.Multiplayer.IsServer)
                    return;

                // Run every search_interval
                if (search_counter <= search_interval)
                        search_counter++;
                else 
                    DoGridSearch();

                if (hud_update_counter <= hud_update_interval)
                    hud_update_counter++;
                else
                    DoHudUpdate();


            }
            catch(Exception e)
            {
                MyLog.Default.WriteLineAndConsole($"{e.Message}\n{e.StackTrace}");

                if(MyAPIGateway.Session?.Player != null)
                    MyAPIGateway.Utilities.ShowNotification($"[ ERROR: {GetType().FullName}: {e.Message} | Send SpaceEngineers.Log to mod author ]", 10000, MyFontEnum.Red);
            }
        }
    }
}


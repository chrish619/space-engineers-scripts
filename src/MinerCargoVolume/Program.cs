using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // This file contains your actual script.
        //
        // You can either keep all your code here, or you can create separate
        // code files to make your program easier to navigate while coding.
        //
        // In order to add a new utility class, right-click on your project, 
        // select 'New' then 'Add Item...'. Now find the 'Space Engineers'
        // category under 'Visual C# Items' on the left hand side, and select
        // 'Utility Class' in the main area. Name it in the box below, and
        // press OK. This utility class will be merged in with your code when
        // deploying your final script.
        //
        // You can also simply create a new utility class manually, you don't
        // have to use the template if you don't want to. Just do so the first
        // time to see what a utility class looks like.
        // 
        // Go to:
        // https://github.com/malware-dev/MDK-SE/wiki/Quick-Introduction-to-Space-Engineers-Ingame-Scripts
        //
        // to learn more about ingame scripts.
        private const string CockpitName = "Industrial Cockpit";

        /*
         * For Industrial Cockpit
         * 0 => TopLeft
         * 1 => TopCenter
         * 2 => TopRight
         */
        private const int DisplayId = 2;

        private const char METER_FILL_CHAR = '|';
        private const char METER_PAD_CHAR = '.';
        private const int METER_CHAR_WIDTH = 20;

        private IMyTextSurface _drawingSurface;
        private RectangleF _viewport;

        public Program()
        {
            // The constructor, called only once every session and
            // always before any other method is called. Use it to
            // initialize your script. 
            //     
            // The constructor is optional and can be removed if not
            // needed.
            // 
            // It's recommended to set Runtime.UpdateFrequency 
            // here, which will allow your script to run itself without a 
            // timer block.
            var cockpit = GridTerminalSystem.GetBlockWithName(CockpitName) as IMyCockpit;

            _drawingSurface = cockpit.GetSurface(DisplayId);
            _drawingSurface.ContentType = ContentType.SCRIPT;
            _drawingSurface.Script = "";

            _viewport = new RectangleF((_drawingSurface.TextureSize - _drawingSurface.SurfaceSize) / 2f, _drawingSurface.SurfaceSize);

            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Save()
        {
            // Called when the program needs to save its state. Use
            // this method to save your state to the Storage field
            // or some other means. 
            // 
            // This method is optional and can be removed if not
            // needed.
        }

        public void Main(string argument, UpdateType updateSource)
        {
            // The main entry point of the script, invoked every time
            // one of the programmable block's Run actions are invoked,
            // or the script updates itself. The updateSource argument
            // describes where the update came from. Be aware that the
            // updateSource is a  bitfield  and might contain more than 
            // one update type.
            // 
            // The method itself is required, but the arguments above
            // can be removed if not needed.

            var drills = new List<IMyShipDrill>();
            var containers = new List<IMyCargoContainer>();

            GridTerminalSystem.GetBlocksOfType<IMyShipDrill>(drills, b => b.IsSameConstructAs(Me));
            GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(containers, b => b.IsSameConstructAs(Me));

            var maxVolume = new MyFixedPoint();
            var currentVolume = new MyFixedPoint();

            var inventories = drills.Cast<IMyEntity>().Concat(containers)
                .Select(e => e.GetInventory());

            var storageDict = new Dictionary<MyItemType, MyFixedPoint>();

            foreach (var inventory in inventories)
            {
                maxVolume += inventory.MaxVolume;
                currentVolume += inventory.CurrentVolume;

                var items = new List<MyInventoryItem>();
                inventory.GetItems(items);

                foreach (var item in items)
                {
                    if (storageDict.ContainsKey(item.Type))
                    {
                        storageDict[item.Type] += item.Amount;
                    }
                    else
                    {
                        storageDict[item.Type] = item.Amount;
                    }
                }
            }

            var frame = _drawingSurface.DrawFrame();

            WriteCargoCapacity(ref frame, storageDict, currentVolume, maxVolume);

            frame.Dispose();
        }

        internal void WriteCargoCapacity(ref MySpriteDrawFrame frame, Dictionary<MyItemType, MyFixedPoint> storageBreakdown, MyFixedPoint currentVolume, MyFixedPoint maxVolume)
        {
            var background = new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Data = "Grid",
                Position = _viewport.Center,
                Size = _viewport.Size,
                Color = Color.White.Alpha(0.66f),
                Alignment = TextAlignment.CENTER,
            };
            frame.Add(background);

            var percVolume = maxVolume > 0 ? ((float)currentVolume / (float)maxVolume) : 0f;
            var position = new Vector2(128, 40) + _viewport.Position;
            var textSprite = new MySprite()
            {
                Type = SpriteType.TEXT,
                Data = $"{percVolume * 100f:N1}%",
                Position = position,
                RotationOrScale = 0.8f,
                Color = Color.White,
                Alignment = TextAlignment.CENTER,
                FontId = "White",
            };
            frame.Add(textSprite);

            var meterVolume = (int)Math.Ceiling(percVolume * METER_CHAR_WIDTH);
            var meterPad = METER_CHAR_WIDTH - meterVolume;

            position = new Vector2(128, 65) + _viewport.Position;
            var barFillSprite = new MySprite()
            {
                Type = SpriteType.TEXT,
                Data = $"[{new string(METER_FILL_CHAR, meterVolume)}{new string(METER_PAD_CHAR, meterPad)}]",
                Position = position,
                RotationOrScale = 0.8f,
                Color = Color.White,
                Alignment = TextAlignment.CENTER,
                FontId = "White",
            };
            frame.Add(barFillSprite);

            var y = 85;
            var totalAmount = storageBreakdown.Aggregate(new MyFixedPoint(), (agg, kvp) => agg + kvp.Value);
            foreach (var stored in storageBreakdown)
            {
                var oreName = GetOreName(stored.Key);
                if (oreName == null)
                {
                    continue;
                }

                y += 20;
                position = new Vector2(32, y) + _viewport.Position;

                var itemPercAmount = (float)stored.Value / (float)totalAmount;
                var itemMeterAmount = (int)Math.Ceiling(itemPercAmount * METER_CHAR_WIDTH);
                var itemMeterPad = METER_CHAR_WIDTH - itemMeterAmount;

                var barItemSprite = new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Data = $"{itemPercAmount * 100f:N1} {oreName}",
                    Position = position,
                    RotationOrScale = 0.8f,
                    Color = Color.White,
                    Alignment = TextAlignment.LEFT,
                    FontId = "White",
                };
                frame.Add(barItemSprite);
            }
        }

        private string GetOreName(MyItemType itemType)
        {
            if (itemType.TypeId != "MyObjectBuilder_Ore") { return null; }

            switch (itemType.SubtypeId)
            {
                case "Cobalt": return "Co";
                case "Gold": return "Au";
                case "Iron": return "Fe";
                case "Magnesium": return "Mg";
                case "Nickel": return "Ni";
                case "Platinum": return "Pt";
                case "Silicon": return "Si";
                case "Silver": return "Ag";
                case "Uranium": return "U";

                default:
                    return itemType.SubtypeId;
            }
        }
    }
}

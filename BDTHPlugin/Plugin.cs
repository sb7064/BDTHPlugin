﻿using Dalamud.Data.LuminaExtensions;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using ImGuiNET;
using ImGuiScene;
using ImGuizmoNET;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace BDTHPlugin
{
    public class Plugin : IDalamudPlugin
    {
        public string Name => "Burning Down the House";

        private const string commandName = "/bdth";

        private DalamudPluginInterface pi;
        private Configuration configuration;
        private PluginUI ui;
        private PluginMemory memory;

        // Sheets used to get housing item info.
        public Dictionary<uint, HousingFurniture> furnitureDict;
        public Dictionary<uint, HousingYardObject> yardObjectDict;

        // Texture dictionary for the housing item icons.
        public readonly Dictionary<ushort, TextureWrap> TextureDictionary = new Dictionary<ushort, TextureWrap>();


        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pi = pluginInterface;

            this.configuration = this.pi.GetPluginConfig() as Configuration ?? new Configuration();
            this.configuration.Initialize(this.pi);

            this.memory = new PluginMemory(this.pi);
            this.ui = new PluginUI(this, this.pi, this.configuration, this.memory);

            // Get the excel sheets for furnishings.
            this.furnitureDict = this.pi.Data.GetExcelSheet<HousingFurniture>().ToDictionary(row => row.RowId, row => row);
            this.yardObjectDict = this.pi.Data.GetExcelSheet<HousingYardObject>().ToDictionary(row => row.RowId, row => row);

            this.pi.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the controls for Burning Down the House plugin."
            });

            // Set the ImGui context 
            ImGuizmo.SetImGuiContext(ImGui.GetCurrentContext());

            this.pi.UiBuilder.OnBuildUi += DrawUI;
        }

        public void Dispose()
        {
            this.ui.Dispose();

            // Dispose everything in the texture dictionary.
            foreach (var t in this.TextureDictionary)
                t.Value?.Dispose();
            this.TextureDictionary.Clear();

            // Dispose for stuff in Plugin Memory class.
            this.memory.Dispose();

            this.pi.CommandManager.RemoveHandler(commandName);
            this.pi.Dispose();
        }

        /// <summary>
        /// Draws icon from game data.
        /// </summary>
        /// <param name="icon"></param>
        /// <param name="size"></param>
        public void DrawIcon(ushort icon, Vector2 size)
		{
            if (icon < 65000)
			{
                if (this.TextureDictionary.ContainsKey(icon))
				{
                    var tex = this.TextureDictionary[icon];
                    if (tex == null || tex.ImGuiHandle == IntPtr.Zero)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1, 0, 0, 1));
                        ImGui.BeginChild("FailedTexture", size);
                        ImGui.Text(icon.ToString());
                        ImGui.EndChild();
                        ImGui.PopStyleColor();
                    }
                    else
                        ImGui.Image(this.TextureDictionary[icon].ImGuiHandle, size);
				}
                else
				{
                    ImGui.BeginChild("WaitingTexture", size, true);
                    ImGui.EndChild();

                    this.TextureDictionary[icon] = null;

                    Task.Run(() =>
                    {
                        try
                        {
                            var iconTex = this.pi.Data.GetIcon(icon);
                            var tex = this.pi.UiBuilder.LoadImageRaw(iconTex.GetRgbaImageData(), iconTex.Header.Width, iconTex.Header.Height, 4);
                            if (tex != null && tex.ImGuiHandle != IntPtr.Zero)
                                this.TextureDictionary[icon] = tex;
                        }
                        catch 
                        {
                        }
                    });
				}
			}
		}

        public bool TryGetFurnishing(uint id, out HousingFurniture furniture) => this.furnitureDict.TryGetValue(id, out furniture);
        public bool TryGetYardObject(uint id, out HousingYardObject furniture) => this.yardObjectDict.TryGetValue(id, out furniture);

        private unsafe void OnCommand(string command, string args)
        {
            args = args.Trim().ToLower();

            // Arguments are being passed in.
            if(!string.IsNullOrEmpty(args))
            {
                // Split the arguments into an array.
                var argArray = args.Split(' ');

                // Check valid state for modifying memory.
                var disabled = !(this.memory.CanEditItem() && this.memory.HousingStructure->ActiveItem != null);

                // Show/Hide the furnishing list.
                if (argArray.Length == 1 && argArray[0].ToLower().Equals("list"))
                {
                    // Only allow furnishing list when the housing window is open.
                    if (!this.memory.IsHousingOpen())
                    {
                        this.pi.Framework.Gui.Chat.PrintError("Cannot open furnishing list unless housing menu is open.");
                        this.ui.ListVisible = false;
                        return;
                    }

                    // Disallow the ability to open furnishing list outdoors.
                    if (this.memory.HousingModule->IsOutdoors())
                    {
                        this.pi.Framework.Gui.Chat.PrintError("Cannot open furnishing outdoors currently.");
                        this.ui.ListVisible = false;
                        return;
                    }

                    this.ui.ListVisible = !this.ui.ListVisible;
                }

                // Position or rotation values are being passed in, and we're not disabled.
                if (argArray.Length >= 3 && !disabled)
                {
                    try
                    {
                        // Parse the coordinates into floats.
                        var x = float.Parse(argArray[0], NumberStyles.Any, CultureInfo.InvariantCulture);
                        var y = float.Parse(argArray[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                        var z = float.Parse(argArray[2], NumberStyles.Any, CultureInfo.InvariantCulture);

                        // Set the position in the memory object.
                        this.memory.position.X = x;
                        this.memory.position.Y = y;
                        this.memory.position.Z = z;

                        // Write the position.
                        this.memory.WritePosition(this.memory.position);

                        // Specifying the rotation as well.
                        if(argArray.Length == 4)
                        {
                            // Parse and write the rotation.
                            this.memory.rotation.Y = (float)(double.Parse(argArray[3]) * 180 / Math.PI);
                            this.memory.WriteRotation(this.memory.rotation);
                        }
                    }
                    catch (Exception ex)
					{
                        PluginLog.LogError(ex, "Error when positioning with command");
					}
                }
            }
            else
            {
                // Hide or show the UI.
                this.ui.Visible = !this.ui.Visible;
            }
        }

        private void DrawUI()
        {
            this.ui.Draw();
        }
    }
}

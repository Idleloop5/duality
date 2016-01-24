﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;

using WeifenLuo.WinFormsUI.Docking;

using AdamsLair.WinForms.ItemModels;

using Duality;
using Duality.Resources;
using Duality.Plugins.Tilemaps;

using Duality.Editor;
using Duality.Editor.Forms;
using Duality.Editor.Properties;
using Duality.Editor.Plugins.Tilemaps.Properties;

namespace Duality.Editor.Plugins.Tilemaps
{
	public class TilemapsEditorPlugin : EditorPlugin
	{
		private static readonly string          ElementNameTilePalette = "TilePalette";
		public static readonly ITileDrawSource EmptyTileDrawingSource = new DummyTileDrawSource();

		private static TilemapsEditorPlugin instance = null;
		public static TilemapsEditorPlugin Instance
		{
			get { return instance; }
		}


		private bool                     isLoading                = false;
		private TilemapToolSourcePalette tilePalette              = null;
		private int                      pendingLocalTilePalettes = 0;
		private XElement                 tilePaletteSettings      = null;
		private ITileDrawSource          tileDrawingSource        = EmptyTileDrawingSource;
		

		public override string Id
		{
			get { return "Tilemaps"; }
		}
		/// <summary>
		/// [GET / SET] The data source that is used for retrieving tile patterns while using a tile drawing tool.
		/// Can be thought of as the "tile brush" that is currently used in user editing operations.
		/// </summary>
		public ITileDrawSource TileDrawingSource
		{
			get { return this.tileDrawingSource; }
			set { this.tileDrawingSource = value ?? EmptyTileDrawingSource; }
		}

		
		public TilemapsEditorPlugin()
		{
			instance = this;
		}

		protected override IDockContent DeserializeDockContent(Type dockContentType)
		{
			this.isLoading = true;
			IDockContent result;
			if (dockContentType == typeof(TilemapToolSourcePalette))
				result = this.RequestTilePalette();
			else
				result = base.DeserializeDockContent(dockContentType);
			this.isLoading = false;
			return result;
		}
		protected override void InitPlugin(MainForm main)
		{
			base.InitPlugin(main);

			// Request menus
			MenuModelItem viewItem = main.MainMenu.RequestItem(GeneralRes.MenuName_View);
			viewItem.AddItem(new MenuModelItem
			{
				Name = TilemapsRes.MenuItemName_TilePalette,
				Icon = TilemapsResCache.IconTilePalette,
				ActionHandler = this.menuItemTilePalette_Click
			});

			// Register events
			FileEventManager.ResourceModified += this.FileEventManager_ResourceModified;
			DualityEditorApp.ObjectPropertyChanged += this.DualityEditorApp_ObjectPropertyChanged;
		}
		protected override void SaveUserData(XElement node)
		{
			// If we have a tile palette, save its settings to the cache node
			if (this.tilePalette != null)
			{
				this.tilePaletteSettings = new XElement(ElementNameTilePalette);
				this.tilePalette.SaveUserData(this.tilePaletteSettings);
			}

			// If there are cached tilemap settings available, save them persistently.
			if (this.tilePaletteSettings != null && !this.tilePaletteSettings.IsEmpty)
			{
				node.Add(new XElement(this.tilePaletteSettings));
			}
		}
		protected override void LoadUserData(XElement node)
		{
			this.isLoading = true;

			// Retrieve tile palette settings from persistent editor data
			foreach (XElement tilePaletteElem in node.Elements(ElementNameTilePalette))
			{
				int i = tilePaletteElem.GetAttributeValue("id", 0);
				if (i < 0 || i >= 1) continue;

				// Cache settings for later
				this.tilePaletteSettings = new XElement(tilePaletteElem);
				break;
			}

			// If we have an active tile palette, apply the settings directly
			if (this.tilePalette != null)
			{
				this.tilePalette.LoadUserData(this.tilePaletteSettings);
			}

			this.isLoading = false;
		}

		/// <summary>
		/// Informs the system that a <see cref="TilemapToolSourcePalette"/> is required and creates one, if none is present yet.
		/// </summary>
		/// <returns></returns>
		public TilemapToolSourcePalette PushTilePalette()
		{
			this.pendingLocalTilePalettes++;
			if (this.pendingLocalTilePalettes == 1)
			{
				this.RequestTilePalette();
			}
			return this.tilePalette;
		}
		/// <summary>
		/// Informs the system that a <see cref="TilemapToolSourcePalette"/> is no longer required and closes it, if no
		/// other claims are pending.
		/// </summary>
		public void PopTilePalette()
		{
			if (this.pendingLocalTilePalettes == 0)
				return;
			else if (this.pendingLocalTilePalettes == 1 && this.tilePalette != null)
				this.tilePalette.Close();
			else
				this.pendingLocalTilePalettes--;
		}

		private TilemapToolSourcePalette RequestTilePalette()
		{
			// Create a new tile palette, if none is available right now
			if (this.tilePalette == null || this.tilePalette.IsDisposed)
			{
				this.tilePalette = new TilemapToolSourcePalette();
				this.tilePalette.FormClosed += this.tilePalette_FormClosed;
			
				// If there are cached settings available, apply them to the new palette
				if (this.tilePaletteSettings != null)
					this.tilePalette.LoadUserData(this.tilePaletteSettings);
			}

			// If we're not creating it as part of the loading procedure, add it to the main docking layout directly
			if (!this.isLoading)
			{
				this.tilePalette.Show(DualityEditorApp.MainForm.MainDockPanel);
			}

			return this.tilePalette;
		}

		private void tilePalette_FormClosed(object sender, FormClosedEventArgs e)
		{
			// Cache tile palette settings for later, so we don't lose them when losing the dock control
			this.tilePaletteSettings = new XElement(ElementNameTilePalette);
			this.tilePalette.SaveUserData(this.tilePaletteSettings);

			// Acknowledge the disposal of our tile palette
			this.tilePalette = null;
			if (e.CloseReason == CloseReason.UserClosing)
			{
				this.pendingLocalTilePalettes--;
			}
		}
		private void menuItemTilePalette_Click(object sender, EventArgs e)
		{
			TilemapToolSourcePalette palette = this.tilePalette ?? this.PushTilePalette();
			if (palette.Pane != null)
			{
				palette.Pane.Activate();
				palette.Focus();
			}
		}
		
		private void FileEventManager_ResourceModified(object sender, ResourceEventArgs e)
		{
			if (e.IsResource) this.OnResourceModified(e.Content);
		}
		private void DualityEditorApp_ObjectPropertyChanged(object sender, ObjectPropertyChangedEventArgs e)
		{
			if (e.Objects.ResourceCount > 0)
			{
				foreach (var r in e.Objects.Resources)
					this.OnResourceModified(r);
			}
		}
		private void OnResourceModified(ContentRef<Resource> resRef)
		{
			List<object> changedObj = null;

			// If a pixmap has been modified, rebuild the tilesets that are based on it.
			if (resRef.Is<Pixmap>())
			{
				ContentRef<Pixmap> pixRef = resRef.As<Pixmap>();
				foreach (ContentRef<Tileset> tilesetRef in ContentProvider.GetLoadedContent<Tileset>())
				{
					Tileset tileset = tilesetRef.Res;

					// Early-out, if the tileset is unavailable, or we didn't compile it yet anyway
					if (tileset == null) continue;
					if (!tileset.Compiled) continue;

					// Determine whether this tileset uses the modified pixmap
					bool usesModifiedPixmap = false;
					foreach (TilesetRenderInput input in tileset.RenderConfig)
					{
						if (input.SourceData == pixRef)
						{
							usesModifiedPixmap = true;
							break;
						}
					}
					if (!usesModifiedPixmap) continue;

					// Recompile the tileset
					tileset.Compile();

					if (changedObj == null) changedObj = new List<object>();
					changedObj.Add(tilesetRef.Res);
				}
			}

			// Notify a change that isn't critical regarding persistence (don't flag stuff unsaved)
			if (changedObj != null)
				DualityEditorApp.NotifyObjPropChanged(this, new ObjectSelection(changedObj as IEnumerable<object>), false);
		}
	}
}

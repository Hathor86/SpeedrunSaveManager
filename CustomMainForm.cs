using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using BizHawk.Client.ApiHawk;
using BizHawk.Client.ApiHawk.Classes.Events;
using BizHawk.Client.Common;
using SpeedrunSaveManager;

namespace BizHawk.Client.EmuHawk
{
	public partial class CustomMainForm : Form, IExternalToolForm
	{
		#region Fields

		private EventHandler romLoadEventHandler;
		private BeforeQuickSaveEventHandler beforeQuickSaveEventHandler;
		private BeforeQuickLoadEventHandler beforeQuickLoadEventHandler;

		#endregion

		public CustomMainForm()
		{
			InitializeComponent();

			romLoadEventHandler = new EventHandler(RomLoad);
			beforeQuickSaveEventHandler = new BeforeQuickSaveEventHandler(BeforeQuickSave);
			beforeQuickLoadEventHandler = new BeforeQuickLoadEventHandler(BeforeQuickLoad);

			RomLoad(null, null);

			treeView.NodeMouseClick += new TreeNodeMouseClickEventHandler(TreeNode_Click);
			treeView.NodeMouseHover += new TreeNodeMouseHoverEventHandler(TreeNode_MouseHover);

			ClientApi.RomLoaded += romLoadEventHandler;
			ClientApi.BeforeQuickSave += beforeQuickSaveEventHandler;
			ClientApi.BeforeQuickLoad += beforeQuickLoadEventHandler;
		}		

		public void RomLoad(object sender, EventArgs e)
		{
			string basePath = Global.Game.Name.AsSafePathName();

			treeView.Nodes.Clear();

			TreeNode main = new TreeNode(basePath);
			treeView.Nodes.Add(main);

			basePath = Path.Combine(PathManager.GetSaveStatePath(Global.Game), basePath);

			if (!Directory.Exists(basePath))
			{
				Directory.CreateDirectory(basePath);
			}
			else
			{
				PopulateTreeView(basePath, main);
			}
			treeView.ExpandAll();
		}

		private void PopulatePictureBox(BinaryReader reader)
		{
			pictureBox1.Image = new Bitmap(reader.BaseStream);
		}

		private void PopulateTreeView(string path, TreeNode node)
		{
			DirectoryInfo current = new DirectoryInfo(path);
			foreach(DirectoryInfo di in current.GetDirectories())
			{
				TreeNode child = new TreeNode(di.Name);
				node.Nodes.Add(child);
				if (di.GetDirectories().Any())
				{
					PopulateTreeView(di.FullName, child);
				}
				else
				{
					foreach(FileInfo savestate in di.GetFiles("*.State"))
					{
						child.Nodes.Add(new TreeNode(savestate.Name.Replace(".State", string.Empty)));
					}
				}
			}
		}

		private void BeforeQuickLoad(object sender, BeforeQuickLoadEventArgs e)
		{
			string path;
			if (treeView.SelectedNode.Nodes.Count == 0 && treeView.SelectedNode.Parent != null)
			{
				treeView.SelectedNode = treeView.SelectedNode.Parent;
			}
			path = Path.Combine(PathManager.GetSaveStatePath(Global.Game), treeView.SelectedNode.FullPath);
			try
			{
				ClientApi.LoadState(Path.Combine(path, e.Name));
			}
			catch (TargetInvocationException)
			{}
			e.Handled = true;
		}

		private void BeforeQuickSave(object sender, BeforeQuickSaveEventArgs e)
		{
			string path;
			if (treeView.SelectedNode.Nodes.Count == 0 && treeView.SelectedNode.Parent != null)
			{
				treeView.SelectedNode = treeView.SelectedNode.Parent;
			}
			path = Path.Combine(PathManager.GetSaveStatePath(Global.Game), treeView.SelectedNode.FullPath);
			ClientApi.SaveState(Path.Combine(path, e.Name));
			e.Handled = true;

			treeView.SelectedNode.Nodes.Add(new TreeNode(e.Name));
		}

		private void TreeNode_Click(object sender, TreeNodeMouseClickEventArgs e)
		{
			treeView.SelectedNode = e.Node;
			if (treeView.SelectedNode.Nodes.Count == 0 && treeView.SelectedNode.Parent != null)
			{
				try
				{
					ClientApi.LoadState(Path.Combine(PathManager.GetSaveStatePath(Global.Game), e.Node.FullPath));
					treeView.SelectedNode = treeView.SelectedNode.Parent;
				}
				catch (TargetInvocationException)
				{}
			}
			nodeStatusLabel.Text = string.Format("Current folder: {0}", treeView.SelectedNode.Text);
		}

		private void TreeNode_MouseHover(object sender, TreeNodeMouseHoverEventArgs e)
		{
			if (e.Node.Nodes.Count == 0)
			{
				BinaryStateLoader.LoadAndDetect(Path.Combine(PathManager.GetSaveStatePath(Global.Game), string.Format("{0}.State", e.Node.FullPath))).GetLump(BinaryStateLump.Framebuffer, false, PopulatePictureBox);
			}
		}

		protected override void OnClosed(EventArgs e)
		{
			base.OnClosed(e);

			ClientApi.BeforeQuickSave -= beforeQuickSaveEventHandler;
			ClientApi.BeforeQuickLoad -= beforeQuickLoadEventHandler;
		}

		#region Bizhawk required stuff
		public bool UpdateBefore
		{
			get
			{
				return true;
			}
		}

		public bool AskSaveChanges()
		{
			return true;
		}

		public void FastUpdate()
		{}

		public void Restart()
		{}

		public void UpdateValues()
		{}
		#endregion
	}
}

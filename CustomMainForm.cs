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
		private ContextMenuStrip menu = new ContextMenuStrip();

		#endregion

		public CustomMainForm()
		{
			InitializeComponent();

			ToolStripMenuItem rename = new ToolStripMenuItem("Rename", null, new EventHandler(MenuItem_Click));
			rename.ShortcutKeys = Keys.F2;
			rename.ShowShortcutKeys = true;

			menu.Items.Add(rename);

			romLoadEventHandler = new EventHandler(RomLoad);
			beforeQuickSaveEventHandler = new BeforeQuickSaveEventHandler(BeforeQuickSave);
			beforeQuickLoadEventHandler = new BeforeQuickLoadEventHandler(BeforeQuickLoad);

			RomLoad(null, null);

			treeView.NodeMouseClick += new TreeNodeMouseClickEventHandler(TreeNode_Click);
			treeView.NodeMouseHover += new TreeNodeMouseHoverEventHandler(TreeNode_MouseHover);
			treeView.AfterLabelEdit += new NodeLabelEditEventHandler(TreeView_AfterLabelEdit);

			ClientApi.RomLoaded += romLoadEventHandler;
			ClientApi.BeforeQuickSave += beforeQuickSaveEventHandler;
			ClientApi.BeforeQuickLoad += beforeQuickLoadEventHandler;
		}

		private void PopulatePictureBox(BinaryReader reader)
		{
			pictureBox1.Image = new Bitmap(reader.BaseStream);
		}

		private void PopulateTreeView(string path, TreeNode node)
		{
			DirectoryInfo current = new DirectoryInfo(path);
			foreach (DirectoryInfo di in current.GetDirectories())
			{
				TreeNode child = new TreeNode(di.Name);
				child.Tag = di;
				node.Nodes.Add(child);

				if (di.GetDirectories().Any())
				{
					PopulateTreeView(di.FullName, child);
				}
				else
				{
					foreach (FileInfo savestate in di.GetFiles("*.State"))
					{
						TreeNode lastNode = new TreeNode(savestate.Name.Replace(".State", string.Empty));
						lastNode.Tag = savestate;
						child.Nodes.Add(lastNode);
					}
				}
			}
		}

		private void BeforeQuickLoad(object sender, BeforeQuickLoadEventArgs e)
		{
			string path;
			if (treeView.SelectedNode.Tag is FileInfo)
			{
				treeView.SelectedNode = treeView.SelectedNode.Parent;
			}
			path = Path.Combine(PathManager.GetSaveStatePath(Global.Game), treeView.SelectedNode.FullPath);
			try
			{
				ClientApi.LoadState(Path.Combine(path, e.Name));
			}
			catch (TargetInvocationException)
			{ }
			e.Handled = true;
		}

		private void BeforeQuickSave(object sender, BeforeQuickSaveEventArgs e)
		{
			string path;

			if (treeView.SelectedNode.Tag is FileInfo)
			{
				treeView.SelectedNode = treeView.SelectedNode.Parent;
			}
			path = Path.Combine(PathManager.GetSaveStatePath(Global.Game), treeView.SelectedNode.FullPath);
			ClientApi.SaveState(Path.Combine(path, e.Name));
			e.Handled = true;

			foreach (TreeNode node in treeView.SelectedNode.Nodes)
			{
				if (node.Text == e.Name)
				{
					return;
				}
			}
			
			treeView.SelectedNode.Nodes.Add(new TreeNode(e.Name));
			treeView.SelectedNode.ExpandAll();
		}

		private void MenuItem_Click(object sender, EventArgs e)
		{
			if (((ToolStripMenuItem)sender).Text == "Rename")
			{
				treeView.SelectedNode.BeginEdit();
			}
		}

		public void RomLoad(object sender, EventArgs e)
		{
			string basePath = Global.Game.Name.AsSafePathName();

			TreeNode main = new TreeNode(basePath);

			basePath = Path.Combine(PathManager.GetSaveStatePath(Global.Game), basePath);

			treeView.Nodes.Clear();

			if (!Directory.Exists(basePath))
			{
				Directory.CreateDirectory(basePath);
			}
			else
			{
				PopulateTreeView(basePath, main);
			}
			main.Tag = new DirectoryInfo(basePath);
			treeView.Nodes.Add(main);

			treeView.ExpandAll();
		}

		private void TreeView_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
		{
			if (e.Label.IndexOfAny(Path.GetInvalidPathChars()) > 0)
			{
				e.CancelEdit = true;
			}
			else
			{
				DirectoryInfo di = e.Node.Tag as DirectoryInfo;
				if (di == null)
				{
					FileInfo fi = (FileInfo)e.Node.Tag;
					string path = Path.Combine(fi.DirectoryName, string.Format("{0}.State", e.Label));
					if (!File.Exists(path))
					{
						fi.MoveTo(path);
					}
					else
					{
						e.CancelEdit = true;
						MessageBox.Show(string.Format("File {0} already exists.", e.Label), "Nope", MessageBoxButtons.OK, MessageBoxIcon.Information);
					}
				}
				else
				{
					string path = Path.Combine(di.Parent.FullName, e.Label);
					if (!Directory.Exists(path))
					{
						//di.MoveTo(path);
					}
					else
					{
						e.CancelEdit = true;
						MessageBox.Show(string.Format("Directory {0} already exists.", e.Label), "Nope", MessageBoxButtons.OK, MessageBoxIcon.Information);
					}
				}
			}
			e.Node.ContextMenuStrip = null;
		}

		private void TreeNode_Click(object sender, TreeNodeMouseClickEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				treeView.SelectedNode = e.Node;
				if (treeView.SelectedNode.Tag is FileInfo)
				{
					try
					{
						ClientApi.LoadState(Path.Combine(PathManager.GetSaveStatePath(Global.Game), e.Node.FullPath));
						treeView.SelectedNode = treeView.SelectedNode.Parent;
					}
					catch (TargetInvocationException)
					{ }
				}
				nodeStatusLabel.Text = string.Format("Current folder: {0}", treeView.SelectedNode.Text);
			}
			else if (e.Button == MouseButtons.Right)
			{
				treeView.SelectedNode = e.Node;
				e.Node.ContextMenuStrip = menu;
			}
		}

		private void TreeNode_MouseHover(object sender, TreeNodeMouseHoverEventArgs e)
		{
			if (e.Node.Tag is FileInfo)
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
		{ }

		public void Restart()
		{ }

		public void UpdateValues()
		{ }
		#endregion
	}
}

using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Reflection;
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

		#region cTor(s)

		public CustomMainForm()
		{
			InitializeComponent();

			ToolStripMenuItem rename = new ToolStripMenuItem("Rename", null, new EventHandler(MenuItem_Click));
			rename.ShortcutKeys = Keys.F2;
			rename.ShowShortcutKeys = true;

			ToolStripMenuItem newDirectory = new ToolStripMenuItem("New directory", null, new EventHandler(MenuItem_Click));

			ToolStripMenuItem delete = new ToolStripMenuItem("Delete", null, new EventHandler(MenuItem_Click));
			delete.ShortcutKeys = Keys.Delete;
			delete.ShowShortcutKeys = true;
			delete.Enabled = false;

			menu.Items.AddRange(new ToolStripItem[] { rename, newDirectory, delete });
			menu.Opening += new CancelEventHandler(Menu_Opening);

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

		#endregion


		#region Methods

		private void DeleteNode()
		{
			bool shouldDelete = false;
			if (treeView.SelectedNode.Tag is DirectoryInfo)
			{
				DirectoryInfo di = (DirectoryInfo)treeView.SelectedNode.Tag;
				if (MessageBox.Show(string.Format("Remove directory {0} and all of its content ?", di.Name), "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
				{
					di.Delete(true);
					shouldDelete = true;
				}

			}
			else
			{
				FileInfo fi = (FileInfo)treeView.SelectedNode.Tag;
				if (MessageBox.Show(string.Format("Remove state {0} ?", fi.Name), "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
				{
					fi.Delete();
					shouldDelete = true;
				}
			}

			if (shouldDelete)
			{
				TreeNode node = treeView.SelectedNode;
				treeView.SelectedNode = treeView.SelectedNode.Parent;
				treeView.SelectedNode.Nodes.Remove(node);
			}
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

				PopulateTreeView(di.FullName, child);

				foreach (FileInfo savestate in di.GetFiles("*.State"))
				{
					TreeNode lastNode = new TreeNode(savestate.Name.Replace(".State", string.Empty));
					lastNode.Tag = savestate;
					child.Nodes.Add(lastNode);
				}
			}
		}

		protected override void OnClosed(EventArgs e)
		{
			base.OnClosed(e);

			ClientApi.BeforeQuickSave -= beforeQuickSaveEventHandler;
			ClientApi.BeforeQuickLoad -= beforeQuickLoadEventHandler;
		}

		#region Event Handlers

		private void BeforeQuickLoad(object sender, BeforeQuickLoadEventArgs e)
		{
			if (treeView.SelectedNode.Tag is FileInfo)
			{
				treeView.SelectedNode = treeView.SelectedNode.Parent;
			}

			string path = Path.Combine(PathManager.GetSaveStatePath(Global.Game), string.Format(@"{0}\{1}", treeView.SelectedNode.FullPath, e.Name));
			if (File.Exists(string.Format("{0}.State", path)))
			{
				try
				{
					ClientApi.LoadState(path);
				}
#if DEBUG
				catch (TargetInvocationException ex)
				{
					MessageBox.Show(ex.Message);
				}
#else
				catch (TargetInvocationException)
				{}
#endif
			}
			e.Handled = true;
		}

		private void BeforeQuickSave(object sender, BeforeQuickSaveEventArgs e)
		{
			if (treeView.SelectedNode == null)
			{
				treeView.SelectedNode = treeView.Nodes[0];
			}

			if (treeView.SelectedNode.Tag is FileInfo)
			{
				treeView.SelectedNode = treeView.SelectedNode.Parent;
			}
			string path = Path.Combine(PathManager.GetSaveStatePath(Global.Game), treeView.SelectedNode.FullPath);
			ClientApi.SaveState(Path.Combine(path, e.Name));
			e.Handled = true;

			foreach (TreeNode node in treeView.SelectedNode.Nodes)
			{
				if (node.Text == e.Name)
				{
					return;
				}
			}

			TreeNode newNode = new TreeNode(e.Name);
			newNode.Tag = new FileInfo(Path.Combine(path, string.Format("{0}.State", e.Name)));
			treeView.SelectedNode.Nodes.Add(newNode);
			if (treeView.SelectedNode != null)
			{
				treeView.SelectedNode.ExpandAll();
			}
		}

		private void MenuItem_Click(object sender, EventArgs e)
		{
			switch (((ToolStripMenuItem)sender).Text)
			{
				case "Rename":
					treeView.SelectedNode.BeginEdit();
					break;

				case "New directory":
					TreeNode newNode = new TreeNode("new");
					treeView.SelectedNode.Nodes.Add(newNode);
					treeView.SelectedNode.Expand();
					newNode.BeginEdit();
					break;

				case "Delete":
					DeleteNode();
					break;

				default:
					break;
			}
		}

		private void Menu_Opening(object sender, CancelEventArgs e)
		{
			if (treeView.SelectedNode.Tag is FileInfo)
			{
				menu.Items[1].Enabled = false;
			}
			else
			{
				menu.Items[1].Enabled = true;
			}
		}

		public void RomLoad(object sender, EventArgs e)
		{
			string basePath = Global.Game.Name.AsSafePathName();
			DirectoryInfo rootDir;

			TreeNode main = new TreeNode(basePath);

			basePath = Path.Combine(PathManager.GetSaveStatePath(Global.Game), basePath);

			treeView.Nodes.Clear();

			if (!Directory.Exists(basePath))
			{
				rootDir = Directory.CreateDirectory(basePath);
			}
			else
			{
				rootDir = new DirectoryInfo(basePath);
				PopulateTreeView(basePath, main);
				foreach (FileInfo savestate in rootDir.GetFiles("*.State"))
				{
					TreeNode lastNode = new TreeNode(savestate.Name.Replace(".State", string.Empty));
					lastNode.Tag = savestate;
					main.Nodes.Add(lastNode);
				}
			}
			main.Tag = new DirectoryInfo(basePath);
			treeView.Nodes.Add(main);

			treeView.ExpandAll();
		}

		private void TreeView_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
		{
			if (e.Label == null || e.Label.Trim() == string.Empty)
			{
				e.CancelEdit = true;
				return;
			}
			if (e.Label != null && e.Label.IndexOfAny(Path.GetInvalidPathChars()) > 0)
			{
				e.CancelEdit = true;
				return;
			}
			else
			{
				if (e.Node.Tag == null) //New node
				{
					DirectoryInfo di = (DirectoryInfo)e.Node.Parent.Tag;
					string path = Path.Combine(di.FullName, e.Label);
					if (!Directory.Exists(path))
					{
						Directory.CreateDirectory(path);
						DirectoryInfo newDirectory = new DirectoryInfo(path);
						e.Node.Tag = newDirectory;
					}
					else
					{
						MessageBox.Show(string.Format("Directory {0} already exists.", e.Label), "Nope", MessageBoxButtons.OK, MessageBoxIcon.Information);
						e.CancelEdit = true;
						e.Node.Parent.Nodes.Remove(e.Node);
					}
				}
				else
				{
					DirectoryInfo di = e.Node.Tag as DirectoryInfo;
					if (di == null) //So, it's a final node, savestate
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
							try
							{
								di.MoveTo(path);
							}
							catch (IOException ex)
							{
								MessageBox.Show(ex.Message);
							}
						}
						else
						{
							e.CancelEdit = true;
							MessageBox.Show(string.Format("Directory {0} already exists.", e.Label), "Nope", MessageBoxButtons.OK, MessageBoxIcon.Information);
						}
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

		#endregion Event Handlers

		#endregion

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

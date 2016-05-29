using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using BizHawk.Client.ApiHawk;
using BizHawk.Client.ApiHawk.Classes.Events;
using BizHawk.Client.Common;

namespace BizHawk.Client.EmuHawk
{
	public partial class CustomMainForm : Form, IExternalToolForm
	{
		#region Fields
		#endregion

		public CustomMainForm()
		{
			InitializeComponent();
			romload(null, null);			

			treeView.NodeMouseClick += new TreeNodeMouseClickEventHandler(TreeNode_Click);
			treeView.NodeMouseHover += new TreeNodeMouseHoverEventHandler(TreeNode_MouseHover);			

			ClientApi.RomLoaded += new EventHandler(romload);
			ClientApi.BeforeQuickSave += new BeforeQuickSaveEventHandler(BeforeQuickSave);
		}		

		public void romload(object sender, EventArgs e)
		{
			string basePath;

			treeView.Nodes.Clear();

			TreeNode main = new TreeNode(Global.Game.Name);
			treeView.Nodes.Add(main);
			/*basePath = Global.Game.Name;
			foreach(char c in Path.GetInvalidPathChars())
			{
				basePath = basePath.Replace(c, '_');
			}*/
			basePath = Path.Combine(PathManager.GetSaveStatePath(Global.Game), Global.Game.Name);

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

		private void BeforeQuickSave(object sender, BeforeQuickSaveEventArgs e)
		{
			string path;
			if(treeView.SelectedNode.Nodes.Count == 0)
			{
				path = Path.Combine(PathManager.GetSaveStatePath(Global.Game), treeView.SelectedNode.FullPath);
			}
			else
			{
				treeView.SelectedNode = treeView.SelectedNode.Parent;
				path = Path.Combine(PathManager.GetSaveStatePath(Global.Game), treeView.SelectedNode.Parent.FullPath);
			}
			ClientApi.SaveState(Path.Combine(path, e.Name));
			e.Handled = true;

			//treeView.SelectedNode.Nodes.Add(new TreeNode(e.Name));
		}

		private void TreeNode_Click(object sender, TreeNodeMouseClickEventArgs e)
		{
			if (treeView.SelectedNode.Nodes.Count == 0)
			{
				ClientApi.LoadState(Path.Combine(PathManager.GetSaveStatePath(Global.Game), e.Node.FullPath));
			}
		}

		private void TreeNode_MouseHover(object sender, TreeNodeMouseHoverEventArgs e)
		{
			if (treeView.SelectedNode.Nodes.Count == 0)
			{
				BinaryStateLoader.LoadAndDetect(Path.Combine(PathManager.GetSaveStatePath(Global.Game), string.Format("{0}.State", e.Node.FullPath))).GetLump(BinaryStateLump.Framebuffer, false, PopulatePictureBox);
			}
		}

		protected override void OnClosed(EventArgs e)
		{
			base.OnClosed(e);

			ClientApi.BeforeQuickSave -= BeforeQuickSave;
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

// 本文件由 Codex 新增

#region 由 Codex 添加
using PIToolKit.Public.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CustomTools.Tools
{
    /// <summary>
    /// 视频查重结果窗口：按组展示可能相同的视频，删除后就地刷新分组，空组自动移除。
    /// </summary>
    internal sealed class VideoCheckWindow : Form
    {
        private readonly TreeView treeView = new TreeView();
        private readonly Button deleteButton = new Button();
        private readonly Button cancelButton = new Button();
        private readonly List<VideoGroup> groups;
        private readonly Dictionary<string, TreeNode> nodeByPath = new Dictionary<string, TreeNode>(StringComparer.OrdinalIgnoreCase);

        public List<string> SelectedFiles { get; } = new List<string>();
        public int DeletedCount { get; private set; }
        public long DeletedBytes { get; private set; }
        public int FailedCount { get; private set; }

        public VideoCheckWindow(List<VideoGroup> groups)
        {
            this.groups = groups;

            Text = "视频查重结果";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(780, 560);
            MinimumSize = new Size(560, 360);
            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = Color.FromArgb(32, 32, 32);
            ForeColor = Color.FromArgb(230, 230, 230);

            treeView.Dock = DockStyle.Fill;
            treeView.HideSelection = false;
            treeView.CheckBoxes = true;
            treeView.BackColor = Color.FromArgb(32, 32, 32);
            treeView.ForeColor = Color.FromArgb(230, 230, 230);
            treeView.NodeMouseClick += TreeView_NodeMouseClick;
            treeView.AfterSelect += TreeView_AfterSelect;
            HandleCreated += (_, _) => ApplyDarkTitleBar();

            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(44, 44, 44)
            };

            deleteButton.Text = "删除选中项";
            deleteButton.Size = new Size(120, 28);
            deleteButton.Location = new Point(10, 10);
            deleteButton.BackColor = Color.FromArgb(70, 70, 70);
            deleteButton.ForeColor = Color.FromArgb(230, 230, 230);
            deleteButton.FlatStyle = FlatStyle.Flat;
            deleteButton.FlatAppearance.BorderColor = Color.FromArgb(110, 110, 110);
            deleteButton.Click += DeleteButton_Click;

            cancelButton.Text = "取消";
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Size = new Size(90, 28);
            cancelButton.Location = new Point(140, 10);
            cancelButton.BackColor = Color.FromArgb(70, 70, 70);
            cancelButton.ForeColor = Color.FromArgb(230, 230, 230);
            cancelButton.FlatStyle = FlatStyle.Flat;
            cancelButton.FlatAppearance.BorderColor = Color.FromArgb(110, 110, 110);

            bottomPanel.Controls.Add(deleteButton);
            bottomPanel.Controls.Add(cancelButton);

            Controls.Add(treeView);
            Controls.Add(bottomPanel);
            Shown += (_, _) => ApplyDarkTitleBar();

            BuildGroups();
        }

        private void BuildGroups()
        {
            treeView.BeginUpdate();
            treeView.Nodes.Clear();
            nodeByPath.Clear();

            for (int i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                var root = new TreeNode($"可能相同的视频组 {i + 1}（{group.Items.Count} 个）");
                for (int j = 0; j < group.Items.Count; j++)
                {
                    var item = group.Items[j];
                    var node = new TreeNode($"{System.IO.Path.GetFileName(item.FilePath)}（{item.Duration:F1}s，{FormatSize(new System.IO.FileInfo(item.FilePath).Length)}）")
                    {
                        Tag = item.FilePath
                    };
                    nodeByPath[item.FilePath] = node;
                    root.Nodes.Add(node);
                }
                treeView.Nodes.Add(root);
            }

            treeView.EndUpdate();
        }

        private void DeleteButton_Click(object? sender, EventArgs e)
        {
            SelectedFiles.Clear();
            foreach (TreeNode root in treeView.Nodes)
            {
                foreach (TreeNode node in root.Nodes)
                {
                    if (node.Checked && node.Tag is string path)
                    {
                        SelectedFiles.Add(path);
                    }
                }
            }

            if (SelectedFiles.Count == 0)
            {
                MessageBox.Show("请先勾选要删除的视频。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"确定删除选中的 {SelectedFiles.Count} 个视频吗？",
                "确认删除",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            var deleted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in SelectedFiles)
            {
                try
                {
                    var length = new System.IO.FileInfo(file).Length;
                    FileUtils.DeleteFile(file);
                    deleted.Add(file);
                    DeletedCount++;
                    DeletedBytes += length;
                }
                catch
                {
                    FailedCount++;
                }
            }

            // 从分组中移除已删除文件，只剩一个文件的组也从界面移除
            foreach (var group in groups)
            {
                group.Items.RemoveAll(item => deleted.Contains(item.FilePath));
            }
            groups.RemoveAll(group => group.Items.Count <= 1);

            BuildGroups();

            // 没有任何分组时自动关闭窗口
            if (groups.Count == 0)
            {
                DialogResult = DialogResult.OK;
            }
        }

        private static string FormatSize(long bytes)
        {
            const long kb = 1024;
            const long mb = 1024 * 1024;
            const long gb = 1024 * 1024 * 1024;
            if (bytes >= gb) return $"{bytes / (double)gb:F2}G";
            if (bytes >= mb) return $"{bytes / (double)mb:F2}M";
            return $"{Math.Max(1, bytes / (double)kb):F0}KB";
        }

        private void ApplyDarkTitleBar()
        {
            var hwnd = Handle;
            var enabled = 1;
            // DWMWA_USE_IMMERSIVE_DARK_MODE，部分旧系统用属性 19
            DwmSetWindowAttribute(hwnd, 20, ref enabled, sizeof(int));
            DwmSetWindowAttribute(hwnd, 19, ref enabled, sizeof(int));
            // 立即刷新窗口边框，避免需要先最小化/最大化才生效
            const uint SWP_NOSIZE = 0x0001;
            const uint SWP_NOMOVE = 0x0002;
            const uint SWP_NOZORDER = 0x0004;
            const uint SWP_NOACTIVATE = 0x0010;
            const uint SWP_FRAMECHANGED = 0x0020;
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOSIZE | SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            // 强制重绘窗口边框，确保深色标题栏立即生效
            const uint RDW_INVALIDATE = 0x0001;
            const uint RDW_UPDATENOW = 0x0100;
            const uint RDW_FRAME = 0x0400;
            const uint RDW_ALLCHILDREN = 0x0080;
            RedrawWindow(hwnd, IntPtr.Zero, IntPtr.Zero,
                RDW_INVALIDATE | RDW_UPDATENOW | RDW_FRAME | RDW_ALLCHILDREN);
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool RedrawWindow(
            IntPtr hWnd,
            IntPtr lprcUpdate,
            IntPtr hrgnUpdate,
            uint flags);

        private void TreeView_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            // 选中分组时，默认勾选除第一个文件外的所有文件
            if (e.Node != null && e.Node.Parent == null)
            {
                var nodes = e.Node.Nodes;
                for (int i = 0; i < nodes.Count; i++)
                {
                    nodes[i].Checked = i > 0;
                }
            }
        }

        private void TreeView_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            // 单击文件名整行也能切换勾选；直接点方框时交给控件自身处理
            if (e.Node?.Parent == null)
            {
                return;
            }

            var hit = treeView.HitTest(e.Location);
            if ((hit.Location & TreeViewHitTestLocations.StateImage) != 0)
            {
                return;
            }

            e.Node.Checked = !e.Node.Checked;
        }
    }
}
#endregion

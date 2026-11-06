// 本文件由 Codex 新增：视频包含查询结果窗口

#region 由 Codex 添加
using PIToolKit.Public.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace CustomTools.Tools
{
    internal sealed class VideoContainsResult
    {
        public string FilePath { get; }
        public double MatchStartSeconds { get; }
        public double Confidence { get; }
        public double AverageDistance { get; }
        public double Duration { get; }

        public VideoContainsResult(
            string filePath,
            double matchStartSeconds,
            double confidence,
            double averageDistance,
            double duration)
        {
            FilePath = filePath;
            MatchStartSeconds = matchStartSeconds;
            Confidence = confidence;
            AverageDistance = averageDistance;
            Duration = duration;
        }
    }

    internal sealed class VideoContainmentGroup
    {
        public string SourceFile { get; }
        public double SourceDuration { get; }
        public List<VideoContainsResult> Items { get; }

        public VideoContainmentGroup(
            string sourceFile,
            double sourceDuration,
            IEnumerable<VideoContainsResult> items)
        {
            SourceFile = sourceFile;
            SourceDuration = sourceDuration;
            Items = items.ToList();
        }
    }

    internal sealed class VideoContainsWindow : Form
    {
        private readonly TreeView treeView = new TreeView();
        private readonly Label summaryLabel = new Label();
        private readonly Button deleteButton = new Button();
        private readonly Button closeButton = new Button();
        private readonly List<VideoContainmentGroup> groups;
        private readonly HashSet<string> deletedCacheDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool updatingChecks;

        #region 由 Codex 添加：结果树右键菜单

        private readonly ContextMenuStrip resultContextMenu = new ContextMenuStrip();
        private readonly ToolStripMenuItem openVideoMenuItem = new ToolStripMenuItem("打开视频");
        private readonly ToolStripMenuItem copyNodeInfoMenuItem = new ToolStripMenuItem("复制节点完整信息");

        #endregion

        public int DeletedCount { get; private set; }
        public long DeletedBytes { get; private set; }
        public int FailedCount { get; private set; }
        public IReadOnlyCollection<string> DeletedCacheDirectories => deletedCacheDirectories;

        public VideoContainsWindow(IReadOnlyList<VideoContainmentGroup> groups)
        {
            this.groups = groups.ToList();

            Text = "视频包含查询结果";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1000, 640);
            MinimumSize = new Size(720, 440);
            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = Color.FromArgb(32, 32, 32);
            ForeColor = Color.FromArgb(230, 230, 230);
            HandleCreated += (_, _) => ApplyDarkTitleBar();

            treeView.Dock = DockStyle.Fill;
            treeView.CheckBoxes = true;
            treeView.HideSelection = false;
            treeView.ShowNodeToolTips = true;
            treeView.BackColor = Color.FromArgb(32, 32, 32);
            treeView.ForeColor = Color.FromArgb(230, 230, 230);
            treeView.AfterCheck += TreeView_AfterCheck;
            treeView.NodeMouseDoubleClick += TreeView_NodeMouseDoubleClick;

            // 由 Codex 修改：播放仅保留右键菜单入口，双击父节点用于勾选分组
            ConfigureResultContextMenu();

            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 76,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(44, 44, 44)
            };
            summaryLabel.Dock = DockStyle.Top;
            summaryLabel.Height = 24;
            summaryLabel.TextAlign = ContentAlignment.MiddleLeft;
            summaryLabel.ForeColor = Color.FromArgb(210, 210, 210);

            deleteButton.Text = "删除选中文件";
            deleteButton.Size = new Size(120, 28);
            deleteButton.Location = new Point(10, 36);
            deleteButton.BackColor = Color.FromArgb(70, 70, 70);
            deleteButton.ForeColor = Color.FromArgb(230, 230, 230);
            deleteButton.FlatStyle = FlatStyle.Flat;
            deleteButton.FlatAppearance.BorderColor = Color.FromArgb(110, 110, 110);
            deleteButton.Click += DeleteButton_Click;

            closeButton.Text = "关闭";
            closeButton.DialogResult = DialogResult.OK;
            closeButton.Size = new Size(90, 28);
            closeButton.Location = new Point(140, 36);
            closeButton.BackColor = Color.FromArgb(70, 70, 70);
            closeButton.ForeColor = Color.FromArgb(230, 230, 230);
            closeButton.FlatStyle = FlatStyle.Flat;
            closeButton.FlatAppearance.BorderColor = Color.FromArgb(110, 110, 110);

            bottomPanel.Controls.Add(summaryLabel);
            bottomPanel.Controls.Add(deleteButton);
            bottomPanel.Controls.Add(closeButton);
            Controls.Add(treeView);
            Controls.Add(bottomPanel);

            BuildTree();
        }

        private void BuildTree()
        {
            updatingChecks = true;
            treeView.BeginUpdate();
            try
            {
                treeView.Nodes.Clear();
                foreach (var group in groups.OrderByDescending(item => item.SourceDuration))
                {
                    var root = new TreeNode(
                        $"源视频：{Path.GetFileName(group.SourceFile)}（{FormatTime(group.SourceDuration)}，包含 {group.Items.Count} 个）")
                    {
                        Tag = group,
                        ToolTipText = group.SourceFile,
                        // 由 Codex 修改：源文件父节点始终不允许勾选
                        Checked = false
                    };

                    foreach (var item in group.Items.OrderByDescending(result => result.Confidence))
                    {
                        var node = new TreeNode(
                            $"{Path.GetFileName(item.FilePath)}  源内位置 {FormatTime(item.MatchStartSeconds)}  置信度 {item.Confidence:P1}  距离 {item.AverageDistance:F1}")
                        {
                            Tag = item,
                            ToolTipText = item.FilePath,
                            // 由 Codex 修改：结果窗口默认选中分组内全部子节点
                            Checked = true
                        };
                        root.Nodes.Add(node);
                    }

                    treeView.Nodes.Add(root);
                }
                treeView.ExpandAll();
            }
            finally
            {
                treeView.EndUpdate();
                updatingChecks = false;
            }

            UpdateSummary();
        }

        private void TreeView_AfterCheck(object? sender, TreeViewEventArgs e)
        {
            if (updatingChecks || e.Node == null)
            {
                return;
            }

            if (e.Node.Parent == null)
            {
                updatingChecks = true;
                try
                {
                    var checkedState = e.Node.Checked;
                    foreach (TreeNode child in e.Node.Nodes)
                    {
                        child.Checked = checkedState;
                    }
                    e.Node.Checked = false;
                }
                finally
                {
                    updatingChecks = false;
                }
            }
            else if (e.Node.Parent.Checked)
            {
                updatingChecks = true;
                try
                {
                    e.Node.Parent.Checked = false;
                }
                finally
                {
                    updatingChecks = false;
                }
            }

            UpdateSummary();
        }

        private void TreeView_NodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            var node = e.Node;
            if (node == null || node.Parent != null)
            {
                return;
            }

            updatingChecks = true;
            treeView.BeginUpdate();
            try
            {
                node.Checked = false;
                foreach (TreeNode child in node.Nodes)
                {
                    child.Checked = true;
                }
            }
            finally
            {
                treeView.EndUpdate();
                updatingChecks = false;
            }
            UpdateSummary();
        }

        #region 由 Codex 添加：结果树节点操作

        private void ConfigureResultContextMenu()
        {
            resultContextMenu.ShowImageMargin = false;
            resultContextMenu.BackColor = Color.FromArgb(44, 44, 44);
            resultContextMenu.ForeColor = Color.FromArgb(230, 230, 230);
            resultContextMenu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColorTable());
            openVideoMenuItem.ForeColor = Color.FromArgb(230, 230, 230);
            copyNodeInfoMenuItem.ForeColor = Color.FromArgb(230, 230, 230);
            openVideoMenuItem.Click += (_, _) => OpenVideoNode(treeView.SelectedNode);
            copyNodeInfoMenuItem.Click += (_, _) => CopySelectedNodeInfo();
            resultContextMenu.Items.Add(openVideoMenuItem);
            resultContextMenu.Items.Add(copyNodeInfoMenuItem);
            resultContextMenu.Opening += ResultContextMenu_Opening;
            treeView.ContextMenuStrip = resultContextMenu;
        }

        private void ResultContextMenu_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            var node = treeView.GetNodeAt(treeView.PointToClient(Cursor.Position));
            if (node != null)
            {
                treeView.SelectedNode = node;
            }

            openVideoMenuItem.Enabled = GetNodeFile(treeView.SelectedNode) != null;
            copyNodeInfoMenuItem.Enabled = treeView.SelectedNode?.Tag != null;
        }

        private static string? GetNodeFile(TreeNode? node)
        {
            return node?.Tag switch
            {
                VideoContainmentGroup group => group.SourceFile,
                VideoContainsResult result => result.FilePath,
                _ => null
            };
        }

        private void OpenVideoNode(TreeNode? node)
        {
            var file = GetNodeFile(node);
            if (file == null)
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(file)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"无法打开视频：{ex.Message}",
                    "视频包含查询",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void CopySelectedNodeInfo()
        {
            var node = treeView.SelectedNode;
            if (node?.Tag == null)
            {
                return;
            }

            try
            {
                Clipboard.SetText(BuildNodeInfo(node));
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"无法复制节点信息：{ex.Message}",
                    "视频包含查询",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static string BuildNodeInfo(TreeNode node)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"节点显示：{node.Text}");
            if (node.Tag is VideoContainmentGroup group)
            {
                builder.AppendLine("节点类型：源视频");
                builder.AppendLine($"完整路径：{group.SourceFile}");
                builder.AppendLine($"视频时长：{FormatExactSeconds(group.SourceDuration)}");
                builder.AppendLine($"包含视频数量：{group.Items.Count}");
                builder.AppendLine("包含视频列表：");
                foreach (var item in group.Items)
                {
                    builder.AppendLine(
                        $"- {item.FilePath} | 源内位置 {FormatExactSeconds(item.MatchStartSeconds)} | 时长 {FormatExactSeconds(item.Duration)} | 置信度 {item.Confidence:P2} | 平均距离 {item.AverageDistance:F2}");
                }
                return builder.ToString().TrimEnd();
            }

            if (node.Tag is VideoContainsResult result)
            {
                var sourceGroup = node.Parent?.Tag as VideoContainmentGroup;
                builder.AppendLine("节点类型：被包含视频");
                builder.AppendLine($"完整路径：{result.FilePath}");
                builder.AppendLine($"所属源视频：{sourceGroup?.SourceFile ?? "未知"}");
                builder.AppendLine($"源视频时长：{FormatExactSeconds(sourceGroup?.SourceDuration ?? 0)}");
                builder.AppendLine($"源内位置：{FormatExactSeconds(result.MatchStartSeconds)}");
                builder.AppendLine($"视频时长：{FormatExactSeconds(result.Duration)}");
                builder.AppendLine($"置信度：{result.Confidence:P2}");
                builder.AppendLine($"平均哈希距离：{result.AverageDistance:F2}");
            }
            return builder.ToString().TrimEnd();
        }

        private static string FormatExactSeconds(double seconds)
        {
            var value = Math.Max(0, seconds);
            return $"{FormatTime(value)}（{value:F3} 秒）";
        }

        private sealed class DarkMenuColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => Color.FromArgb(44, 44, 44);
            public override Color MenuItemSelected => Color.FromArgb(70, 70, 70);
            public override Color MenuItemBorder => Color.FromArgb(110, 110, 110);
            public override Color MenuBorder => Color.FromArgb(110, 110, 110);
            public override Color ImageMarginGradientBegin => Color.FromArgb(44, 44, 44);
            public override Color ImageMarginGradientMiddle => Color.FromArgb(44, 44, 44);
            public override Color ImageMarginGradientEnd => Color.FromArgb(44, 44, 44);
        }

        #endregion

        private void DeleteButton_Click(object? sender, EventArgs e)
        {
            var selected = treeView.Nodes
                .Cast<TreeNode>()
                .SelectMany(root => root.Nodes.Cast<TreeNode>())
                .Where(node => node.Checked && node.Tag is VideoContainsResult)
                .Select(node => (VideoContainsResult)node.Tag!)
                .DistinctBy(result => result.FilePath, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (selected.Length == 0)
            {
                MessageBox.Show("请先勾选要删除的被包含视频。", "视频包含查询", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"确定永久删除选中的 {selected.Length} 个视频吗？删除后无法恢复。",
                "确认删除",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            var deletedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var deletedCacheIdentities = new List<(string Directory, long Length, long Ticks)>();

            // 由 Codex 修改：删除前记录文件标识，成功后同步移除对应 pHash 缓存
            foreach (var result in selected)
            {
                try
                {
                    var info = new FileInfo(result.FilePath);
                    var directory = Path.GetFullPath(Path.GetDirectoryName(result.FilePath) ?? ".");
                    var identity = (Length: info.Length, Ticks: info.LastWriteTimeUtc.Ticks);
                    FileUtils.DeleteFile(result.FilePath);
                    if (File.Exists(result.FilePath))
                    {
                        throw new IOException("文件仍然存在");
                    }
                    deletedPaths.Add(result.FilePath);
                    deletedCacheIdentities.Add((directory, identity.Length, identity.Ticks));
                    DeletedCount++;
                    DeletedBytes += identity.Length;
                }
                catch
                {
                    FailedCount++;
                }
            }

            var cacheCleanupFailed = RemoveDeletedFileCaches(
                deletedCacheIdentities,
                deletedCacheDirectories);

            foreach (var group in groups)
            {
                group.Items.RemoveAll(item => deletedPaths.Contains(item.FilePath));
            }
            groups.RemoveAll(group => group.Items.Count == 0);

            BuildTree();
            if (FailedCount > 0 || cacheCleanupFailed)
            {
                var message = $"已删除 {DeletedCount} 个文件";
                if (FailedCount > 0)
                {
                    message += $"，{FailedCount} 个删除失败";
                }
                if (cacheCleanupFailed)
                {
                    message += "，但部分 pHash 缓存清理失败";
                }
                MessageBox.Show(
                    $"{message}。",
                    "视频包含查询",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            if (groups.Count == 0)
            {
                DialogResult = DialogResult.OK;
            }
        }

        private static bool RemoveDeletedFileCaches(
            IEnumerable<(string Directory, long Length, long Ticks)> identities,
            HashSet<string> successfulDirectories)
        {
            var failed = false;
            foreach (var group in identities.GroupBy(item => item.Directory, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    VideoCheckCache.RemoveEntries(
                        group.Key,
                        group.Select(item => (item.Length, item.Ticks)));
                    successfulDirectories.Add(group.Key);
                }
                catch
                {
                    failed = true;
                }
            }
            return failed;
        }

        private void UpdateSummary()
        {
            var containedCount = groups.Sum(group => group.Items.Count);
            var selectedCount = treeView.Nodes
                .Cast<TreeNode>()
                .SelectMany(root => root.Nodes.Cast<TreeNode>())
                .Count(node => node.Checked);
            summaryLabel.Text =
                $"源视频：{groups.Count} 个    被包含视频：{containedCount} 个    当前选中：{selectedCount} 个";
            deleteButton.Enabled = selectedCount > 0;
        }

        private static string FormatTime(double seconds)
        {
            if (seconds < 0)
            {
                seconds = 0;
            }

            var time = TimeSpan.FromSeconds(seconds);
            return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
        }

        private void ApplyDarkTitleBar()
        {
            var enabled = 1;
            DwmSetWindowAttribute(Handle, 20, ref enabled, sizeof(int));
            DwmSetWindowAttribute(Handle, 19, ref enabled, sizeof(int));
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
#endregion

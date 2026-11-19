// 本文件由 GPT-5 新增：图片查重结果窗口，按缩略图分组并支持批量删除

#region 由 GPT-5 添加
using PIToolKit.Public.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CustomTools.Tools
{
    /// <summary>
    /// 图片查重结果窗口：按组展示缩略图，删除文件后自动刷新剩余分组。
    /// </summary>
    internal sealed class ImageCheckWindow : Form
    {
        // 由 GPT-5 修改：缩小结果窗口缩略图并统一行高

        private const int ThumbnailSize = 64;
        private const int ThumbnailRowHeight = 72;

        private readonly TreeView treeView = new TreeView();
        private readonly ImageList thumbnails = new ImageList();
        private readonly Button deleteButton = new Button();
        private readonly Button selectNonFirstButton = new Button();
        private readonly Button cancelButton = new Button();
        private readonly Label summaryLabel = new Label();
        private readonly ContextMenuStrip resultContextMenu = new ContextMenuStrip();
        private readonly ToolStripMenuItem openImageMenuItem = new ToolStripMenuItem("打开图片");
        private readonly ToolStripMenuItem openFolderMenuItem = new ToolStripMenuItem("打开所在文件夹");
        private readonly ToolStripMenuItem copyPathMenuItem = new ToolStripMenuItem("复制完整路径");
        private readonly List<ImageGroup> groups;
        private readonly Dictionary<string, TreeNode> nodeByPath =
            new Dictionary<string, TreeNode>(StringComparer.OrdinalIgnoreCase);
        private readonly int threshold;
        private bool updatingChecks;

        public List<string> SelectedFiles { get; } = new List<string>();
        public int DeletedCount { get; private set; }
        public long DeletedBytes { get; private set; }
        public int FailedCount { get; private set; }

        public ImageCheckWindow(List<ImageGroup> groups, int threshold)
        {
            this.groups = groups;
            this.threshold = threshold;

            Text = "图片查重结果";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(980, 680);
            MinimumSize = new Size(700, 460);
            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = Color.FromArgb(32, 32, 32);
            ForeColor = Color.FromArgb(230, 230, 230);
            HandleCreated += (_, _) => ApplyDarkTitleBar();

            thumbnails.ColorDepth = ColorDepth.Depth32Bit;
            thumbnails.ImageSize = new Size(ThumbnailSize, ThumbnailSize);

            treeView.Dock = DockStyle.Fill;
            treeView.HideSelection = false;
            treeView.CheckBoxes = true;
            treeView.ShowNodeToolTips = true;
            treeView.ItemHeight = ThumbnailRowHeight;
            treeView.ImageList = thumbnails;
            treeView.BackColor = Color.FromArgb(32, 32, 32);
            treeView.ForeColor = Color.FromArgb(230, 230, 230);
            treeView.NodeMouseClick += TreeView_NodeMouseClick;
            treeView.AfterSelect += TreeView_AfterSelect;
            treeView.AfterCheck += TreeView_AfterCheck;
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

            deleteButton.Text = "删除选中项";
            deleteButton.Size = new Size(120, 28);
            deleteButton.Location = new Point(10, 36);
            ConfigureButton(deleteButton);
            deleteButton.Click += DeleteButton_Click;

            selectNonFirstButton.Text = "选中每组非首个";
            selectNonFirstButton.Size = new Size(140, 28);
            selectNonFirstButton.Location = new Point(140, 36);
            ConfigureButton(selectNonFirstButton);
            selectNonFirstButton.Click += SelectNonFirstButton_Click;

            cancelButton.Text = "关闭";
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Size = new Size(90, 28);
            cancelButton.Location = new Point(290, 36);
            ConfigureButton(cancelButton);

            bottomPanel.Controls.Add(summaryLabel);
            bottomPanel.Controls.Add(deleteButton);
            bottomPanel.Controls.Add(selectNonFirstButton);
            bottomPanel.Controls.Add(cancelButton);

            Controls.Add(treeView);
            Controls.Add(bottomPanel);
            Shown += (_, _) => ApplyDarkTitleBar();

            BuildGroups();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ClearThumbnails();
                thumbnails.Dispose();
                resultContextMenu.Dispose();
            }
            base.Dispose(disposing);
        }

        private static void ConfigureButton(Button button)
        {
            button.BackColor = Color.FromArgb(70, 70, 70);
            button.ForeColor = Color.FromArgb(230, 230, 230);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(110, 110, 110);
        }

        private void ConfigureResultContextMenu()
        {
            resultContextMenu.ShowImageMargin = false;
            resultContextMenu.BackColor = Color.FromArgb(44, 44, 44);
            resultContextMenu.ForeColor = Color.FromArgb(230, 230, 230);
            resultContextMenu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColorTable());
            openImageMenuItem.ForeColor = Color.FromArgb(230, 230, 230);
            openFolderMenuItem.ForeColor = Color.FromArgb(230, 230, 230);
            copyPathMenuItem.ForeColor = Color.FromArgb(230, 230, 230);
            openImageMenuItem.Click += (_, _) => OpenSelectedImage();
            openFolderMenuItem.Click += (_, _) => OpenSelectedFolder();
            copyPathMenuItem.Click += (_, _) => CopySelectedPath();
            resultContextMenu.Items.Add(openImageMenuItem);
            resultContextMenu.Items.Add(openFolderMenuItem);
            resultContextMenu.Items.Add(copyPathMenuItem);
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

            var hasFile = GetNodeFile(treeView.SelectedNode) != null;
            openImageMenuItem.Enabled = hasFile;
            openFolderMenuItem.Enabled = hasFile;
            copyPathMenuItem.Enabled = hasFile;
        }

        private static string? GetNodeFile(TreeNode? node)
        {
            return node?.Tag is ImageResult result ? result.FilePath : null;
        }

        private void OpenSelectedImage()
        {
            var file = GetNodeFile(treeView.SelectedNode);
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
                    $"无法打开图片：{ex.Message}",
                    "图片查重",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void OpenSelectedFolder()
        {
            var file = GetNodeFile(treeView.SelectedNode);
            if (file == null)
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file}\"")
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"无法打开所在文件夹：{ex.Message}",
                    "图片查重",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void CopySelectedPath()
        {
            var file = GetNodeFile(treeView.SelectedNode);
            if (file == null)
            {
                return;
            }

            try
            {
                Clipboard.SetText(file);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"无法复制完整路径：{ex.Message}",
                    "图片查重",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void BuildGroups()
        {
            updatingChecks = true;
            treeView.BeginUpdate();
            try
            {
                treeView.Nodes.Clear();
                nodeByPath.Clear();
                ClearThumbnails();

                for (var i = 0; i < groups.Count; i++)
                {
                    var group = groups[i];
                    var root = new TreeNode($"可能相同的图片组 {i + 1}（{group.Items.Count} 个）");
                    var groupImageIndex = -1;
                    foreach (var item in group.Items.OrderByDescending(GetFileLength))
                    {
                        var imageIndex = AddThumbnail(item.FilePath);
                        if (groupImageIndex < 0 && imageIndex >= 0)
                        {
                            groupImageIndex = imageIndex;
                        }
                        var node = new TreeNode(
                            $"{Path.GetFileName(item.FilePath)}（{FormatSize(GetFileLength(item))}）")
                        {
                            Tag = item,
                            ToolTipText = item.FilePath,
                            ImageIndex = imageIndex,
                            SelectedImageIndex = imageIndex
                        };
                        nodeByPath[item.FilePath] = node;
                        root.Nodes.Add(node);
                    }
                    root.ImageIndex = groupImageIndex;
                    root.SelectedImageIndex = groupImageIndex;
                    treeView.Nodes.Add(root);
                }
            }
            finally
            {
                treeView.EndUpdate();
                updatingChecks = false;
            }

            treeView.ExpandAll();
            UpdateSummaryLabel();
        }

        private int AddThumbnail(string file)
        {
            try
            {
                using var stream = new FileStream(
                    file,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var source = Image.FromStream(stream, false, true);
                var bitmap = new Bitmap(ThumbnailSize, ThumbnailSize, PixelFormat.Format32bppArgb);
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.FromArgb(24, 24, 24));
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    var scale = Math.Min(
                        (double)ThumbnailSize / source.Width,
                        (double)ThumbnailSize / source.Height);
                    var width = Math.Max(1, (int)Math.Round(source.Width * scale));
                    var height = Math.Max(1, (int)Math.Round(source.Height * scale));
                    var x = (ThumbnailSize - width) / 2;
                    var y = (ThumbnailSize - height) / 2;
                    graphics.DrawImage(source, new Rectangle(x, y, width, height));
                }

                thumbnails.Images.Add(bitmap);
                return thumbnails.Images.Count - 1;
            }
            catch
            {
                return -1;
            }
        }

        private void ClearThumbnails()
        {
            while (thumbnails.Images.Count > 0)
            {
                var image = thumbnails.Images[0];
                thumbnails.Images.RemoveAt(0);
                image?.Dispose();
            }
        }

        private void SelectNonFirstButton_Click(object? sender, EventArgs e)
        {
            var previousUpdatingChecks = updatingChecks;
            updatingChecks = true;
            treeView.BeginUpdate();
            try
            {
                foreach (TreeNode root in treeView.Nodes)
                {
                    for (var i = 0; i < root.Nodes.Count; i++)
                    {
                        root.Nodes[i].Checked = i > 0;
                    }
                }
            }
            finally
            {
                treeView.EndUpdate();
                updatingChecks = previousUpdatingChecks;
            }
            UpdateSummaryLabel();
        }

        private void TreeView_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            if (e.Node == null || e.Node.Parent != null)
            {
                return;
            }

            var previousUpdatingChecks = updatingChecks;
            updatingChecks = true;
            treeView.BeginUpdate();
            try
            {
                for (var i = 0; i < e.Node.Nodes.Count; i++)
                {
                    e.Node.Nodes[i].Checked = i > 0;
                }
            }
            finally
            {
                treeView.EndUpdate();
                updatingChecks = previousUpdatingChecks;
            }
            UpdateSummaryLabel();
        }

        private void TreeView_AfterCheck(object? sender, TreeViewEventArgs e)
        {
            if (!updatingChecks)
            {
                UpdateSummaryLabel();
            }
        }

        private void TreeView_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
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

        private void DeleteButton_Click(object? sender, EventArgs e)
        {
            SelectedFiles.Clear();
            foreach (TreeNode root in treeView.Nodes)
            {
                foreach (TreeNode node in root.Nodes)
                {
                    if (node.Checked && node.Tag is ImageResult result)
                    {
                        SelectedFiles.Add(result.FilePath);
                    }
                }
            }

            if (SelectedFiles.Count == 0)
            {
                MessageBox.Show(
                    "请先勾选要删除的图片。",
                    "提示",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"确定删除选中的 {SelectedFiles.Count} 个图片吗？",
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
                    var length = GetFileLength(new ImageResult(file, Array.Empty<byte>()));
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

            foreach (var group in groups)
            {
                group.Items.RemoveAll(item => deleted.Contains(item.FilePath));
            }
            groups.RemoveAll(group => group.Items.Count <= 1);

            BuildGroups();
            if (groups.Count == 0)
            {
                DialogResult = DialogResult.OK;
            }
        }

        private void UpdateSummaryLabel()
        {
            var possibleDuplicates = groups.Sum(group => group.Items.Count);
            var selectedCount = nodeByPath.Values.Count(node => node.Checked);
            summaryLabel.Text =
                $"重复组：{groups.Count} 个    可能重复图片：{possibleDuplicates} 个    当前选中：{selectedCount} 个    相似阈值：{threshold}";
        }

        private static long GetFileLength(ImageResult item)
        {
            try
            {
                return new FileInfo(item.FilePath).Length;
            }
            catch
            {
                return 0;
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
            var enabled = 1;
            DwmSetWindowAttribute(Handle, 20, ref enabled, sizeof(int));
            DwmSetWindowAttribute(Handle, 19, ref enabled, sizeof(int));
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

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
    }
}
#endregion

using System.Numerics;
using System.Drawing.Imaging;

namespace MountainGen.WinForms;

public sealed class MainForm : Form
{
    private readonly RenderSettings settings = new();
    private Terrain terrain = null!;
    private readonly PictureBox preview = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Normal, BackColor = Color.Black };
    private readonly FlowLayoutPanel controls = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(14), BackColor = Color.FromArgb(34,43,58) };
    private readonly Label status = new() { Dock=DockStyle.Bottom, Height=28, TextAlign=ContentAlignment.MiddleLeft, Padding=new Padding(12,0,0,0) };
    private readonly NumericUpDown detail = Number(6,2,9);
    private readonly NumericUpDown seed = Number(2002,0,int.MaxValue);
    private readonly NumericUpDown roughness = Number(1,0,2,2,.05m);
    private readonly NumericUpDown seaLevel = Number(0,-1,1,2,.05m);
    private readonly NumericUpDown elevation = Number(34,5,85);
    private readonly NumericUpDown azimuth = Number(42,-360,360);
    private readonly NumericUpDown zoom = Number(1,.2m,3,2,.1m);
    private readonly NumericUpDown heightScale = Number(1,.2m,3,2,.1m);
    private readonly CheckBox wireframe = new() { Text="Wireframe", AutoSize=true };
    private readonly CheckBox water = new() { Text="Show water", Checked=true, AutoSize=true };
    private readonly CheckBox axes = new() { Text="Show axes", AutoSize=true };
    private readonly System.Windows.Forms.Timer redrawTimer = new() { Interval=100 };

    public MainForm()
    {
        Text = "MountainGen — C# Windows Forms";
        MinimumSize = new Size(850,580);
        ClientSize = new Size(1280,820);
        Font = new Font("Segoe UI", 9);
        BackColor=Color.FromArgb(27,34,46);
        ForeColor=Color.WhiteSmoke;
        var layout=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=2, RowCount=1, Margin=Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,268));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        Controls.Add(layout);
        Controls.Add(status);
        layout.Controls.Add(controls,0,0);
        layout.Controls.Add(preview,1,0);
        AddHeader("TERRAIN");
        AddField("Detail (2–9)",detail);
        AddField("Random seed",seed);
        AddField("Roughness",roughness);
        AddButton("Generate new terrain",()=>Generate());
        AddButton("Randomise seed",()=> { seed.Value=Random.Shared.Next(0,int.MaxValue); Generate(); });
        AddHeader("VIEW");
        AddField("Azimuth (degrees)",azimuth);
        AddField("Elevation (degrees)",elevation);
        AddField("Zoom",zoom);
        AddField("Height scale",heightScale);
        AddField("Water level",seaLevel);
        controls.Controls.Add(wireframe);
        controls.Controls.Add(water);
        controls.Controls.Add(axes);
        AddHeader("OUTPUT");
        AddButton("Save image as PNG / BMP",SaveImage);
        AddButton("Export mesh as OBJ",ExportObj);
        AddButton("Reset camera",()=> { azimuth.Value=42; elevation.Value=34; zoom.Value=1; });
        controls.Controls.Add(new Label { Text="Drag over image to orbit. Mouse wheel to zoom.", Width=220, Height=44, ForeColor=Color.LightGray });
        foreach (var numeric in new[] { azimuth,elevation,zoom,heightScale,seaLevel }) numeric.ValueChanged += (_,_)=>QueueRender();
        foreach (var check in new[] { wireframe,water,axes }) check.CheckedChanged+=(_,_)=>QueueRender();
        redrawTimer.Tick += (_,_)=> {redrawTimer.Stop(); Render();};
        preview.Resize += (_,_)=>QueueRender();
        Point? lastMouse=null;
        preview.MouseDown += (_,e)=> {if(e.Button==MouseButtons.Left) lastMouse=e.Location;};
        preview.MouseUp += (_,_)=>lastMouse=null;
        preview.MouseMove += (_,e)=>
        {
            if(lastMouse is not Point previous) return;
            azimuth.Value=Math.Clamp(azimuth.Value+(e.X-previous.X)*.7m,azimuth.Minimum,azimuth.Maximum);
            elevation.Value=Math.Clamp(elevation.Value-(e.Y-previous.Y)*.4m,elevation.Minimum,elevation.Maximum);
            lastMouse=e.Location;
        };
        preview.MouseWheel += (_,e)=>zoom.Value=Math.Clamp(zoom.Value+(e.Delta>0?.1m:-.1m),zoom.Minimum,zoom.Maximum);
        Shown += (_,_)=>Generate();
    }

    private static NumericUpDown Number(decimal value,decimal min,decimal max,int decimals=0,decimal increment=1)
        => new() { Minimum=min,Maximum=max,Value=value,DecimalPlaces=decimals,Increment=increment, Width=218,Height=28 };
    private void AddHeader(string title)
        => controls.Controls.Add(new Label { Text=title, Width=220,Height=30,TextAlign=ContentAlignment.BottomLeft,ForeColor=Color.LightSkyBlue,Font=new Font(Font,FontStyle.Bold),Margin=new Padding(0,14,0,5) });
    private void AddField(string title, Control control)
    {
        controls.Controls.Add(new Label { Text=title, AutoSize=false,Width=220,Height=21,Margin=new Padding(0,8,0,0) });
        controls.Controls.Add(control);
    }
    private void AddButton(string title,Action action)
    {
        var button=new Button { Text=title,Width=220,Height=36,Margin=new Padding(0,7,0,2),FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(60,87,116),ForeColor=Color.White };
        button.Click+=(_,_)=>action();
        controls.Controls.Add(button);
    }
    private void Generate()
    {
        try
        {
            UseWaitCursor=true;
            terrain=Terrain.Generate((int)detail.Value,(int)seed.Value,(float)roughness.Value);
            Render();
        }
        catch(Exception ex) { MessageBox.Show(this,ex.Message,"Generation error"); }
        finally {UseWaitCursor=false;}
    }
    private void QueueRender() { redrawTimer.Stop(); redrawTimer.Start(); }
    private void UpdateSettings()
    {
        settings.Azimuth=(float)azimuth.Value;
        settings.Elevation=(float)elevation.Value;
        settings.Zoom=(float)zoom.Value;
        settings.HeightScale=(float)heightScale.Value;
        settings.WaterLevel=(float)seaLevel.Value;
        settings.Wireframe=wireframe.Checked;
        settings.ShowWater=water.Checked;
        settings.ShowAxes=axes.Checked;
    }
    private void Render()
    {
        if(terrain is null || preview.Width<2 || preview.Height<2) return;
        try
        {
            UpdateSettings();
            var bmp=TerrainRenderer.Render(terrain,settings,preview.Width,preview.Height);
            var old=preview.Image;
            preview.Image=bmp;
            old?.Dispose();
            int cells=terrain.Size-1;
            status.Text=$"Grid: {terrain.Size}×{terrain.Size}  |  Triangles: {2*cells*cells:N0}  |  Seed: {seed.Value}";
        }
        catch(Exception ex) {status.Text="Rendering error: "+ex.Message;}
    }
    private void SaveImage()
    {
        if(preview.Image is null)return;
        using var dlg=new SaveFileDialog { Filter="PNG image|*.png|Bitmap image|*.bmp",FileName="mountain.png" };
        if(dlg.ShowDialog(this)!=DialogResult.OK)return;
        var format=dlg.FilterIndex==2?ImageFormat.Bmp:ImageFormat.Png;
        preview.Image.Save(dlg.FileName,format);
    }
    private void ExportObj()
    {
        if(terrain is null)return;
        using var dlg=new SaveFileDialog { Filter="Wavefront OBJ|*.obj",FileName="mountain.obj" };
        if(dlg.ShowDialog(this)!=DialogResult.OK)return;
        using var writer=new StreamWriter(dlg.FileName);
        writer.WriteLine("# MountainGen WinForms terrain mesh");
        int n=terrain.Size;
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)
        {
            float h=terrain.Heights[x,z]*settings.HeightScale;
            if(settings.ShowWater)h=Math.Max(h,settings.WaterLevel);
            writer.WriteLine(FormattableString.Invariant($"v {(float)x/(n-1)*2-1:F6} {h:F6} {(float)z/(n-1)*2-1:F6}"));
        }
        for(int z=0;z<n-1;z++)for(int x=0;x<n-1;x++)
        {
            int a=z*n+x+1,b=a+1,c=a+n,d=c+1;
            writer.WriteLine($"f {a} {c} {b}");
            writer.WriteLine($"f {b} {c} {d}");
        }
    }
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        redrawTimer.Dispose();
        preview.Image?.Dispose();
        base.OnFormClosed(e);
    }
}
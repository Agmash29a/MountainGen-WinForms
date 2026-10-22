using System.Drawing.Drawing2D;
using System.Numerics;

namespace MountainGen.WinForms;

public sealed class RenderSettings
{
    public bool Wireframe { get; set; }
    public bool ShowWater { get; set; } = true;
    public bool ShowAxes { get; set; }
    public float WaterLevel { get; set; } = 0f;
    public float Elevation { get; set; } = 34f;
    public float Azimuth { get; set; } = 42f;
    public float Zoom { get; set; } = 1f;
    public float HeightScale { get; set; } = 1f;
    public Vector3 LightDirection { get; set; } = Vector3.Normalize(new Vector3(-0.6f, 1.2f, 0.8f));
    public Color Snow { get; set; } = Color.FromArgb(239,244,247);
    public Color Vegetation { get; set; } = Color.FromArgb(60,133,74);
    public Color Rock { get; set; } = Color.FromArgb(132,126,112);
    public Color Water { get; set; } = Color.FromArgb(49,116,177);
}

public static class TerrainRenderer
{
    private readonly record struct Face(PointF A, PointF B, PointF C, float Depth, Color Color);

    public static Bitmap Render(Terrain terrain, RenderSettings settings, int width, int height)
    {
        width = Math.Max(width, 2); height = Math.Max(height, 2);
        var bitmap = new Bitmap(width, height);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var bg = new LinearGradientBrush(new Rectangle(0,0,width,height),
                   Color.FromArgb(21,34,55), Color.FromArgb(117,155,182), 90f))
            g.FillRectangle(bg, 0, 0, width, height);

        int n = terrain.Size;
        float sinA = MathF.Sin(settings.Azimuth*MathF.PI/180f);
        float cosA = MathF.Cos(settings.Azimuth*MathF.PI/180f);
        float sinE = MathF.Sin(settings.Elevation*MathF.PI/180f);
        float cosE = MathF.Cos(settings.Elevation*MathF.PI/180f);
        float scale = Math.Min(width, height) * 0.61f * settings.Zoom;
        var projected = new PointF[n,n];
        var depth = new float[n,n];
        var positions = new Vector3[n,n];
        for (int z = 0; z < n; z++)
        for (int x = 0; x < n; x++)
        {
            float xx = (float)x/(n-1)*2-1;
            float zz = (float)z/(n-1)*2-1;
            float yy = terrain.Heights[x,z] * settings.HeightScale;
            if (settings.ShowWater) yy = Math.Max(yy, settings.WaterLevel);
            positions[x,z] = new Vector3(xx,yy,zz);
            float rx = cosA*xx - sinA*zz;
            float rz = sinA*xx + cosA*zz;
            float ry = cosE*yy - sinE*rz;
            depth[x,z] = sinE*yy + cosE*rz;
            projected[x,z] = new PointF(width/2f + rx*scale, height*.58f - ry*scale);
        }

        var faces = new List<Face>(2*(n-1)*(n-1));
        for (int z = 0; z < n-1; z++)
        for (int x = 0; x < n-1; x++)
        {
            AddFace(x,z, x+1,z, x,z+1);
            AddFace(x+1,z, x+1,z+1, x,z+1);
        }
        // Painter's algorithm: draw distant triangles before near triangles.
        faces.Sort((a,b) => b.Depth.CompareTo(a.Depth));
        using var wirePen = new Pen(Color.FromArgb(210,230,239), .6f);
        foreach (var face in faces)
        {
            PointF[] polygon = [face.A, face.B, face.C];
            if (!settings.Wireframe)
            {
                using var brush = new SolidBrush(face.Color);
                g.FillPolygon(brush, polygon);
            }
            else g.DrawPolygon(wirePen, polygon);
        }
        if (settings.ShowAxes)
        {
            DrawAxis(new Vector3(-1,0,-1), new Vector3(1,0,-1), Pens.Tomato);
            DrawAxis(new Vector3(-1,0,-1), new Vector3(-1,1,-1), Pens.LightGreen);
            DrawAxis(new Vector3(-1,0,-1), new Vector3(-1,0,1), Pens.CornflowerBlue);
        }
        return bitmap;

        void AddFace(int ax,int az,int bx,int bz,int cx,int cz)
        {
            var a=positions[ax,az]; var b=positions[bx,bz]; var c=positions[cx,cz];
            Vector3 normal = Vector3.Cross(b-a,c-a);
            if (normal.LengthSquared()<1e-12f) normal=Vector3.UnitY;
            else normal=Vector3.Normalize(normal);
            if(normal.Y<0) normal=-normal;
            float brightness = Math.Clamp(0.35f+0.75f*Math.Max(0,Vector3.Dot(normal,settings.LightDirection)),0.25f,1.1f);
            float altitude=(a.Y+b.Y+c.Y)/3;
            Color baseColor;
            if(settings.ShowWater && altitude <= settings.WaterLevel+0.0001f)
                baseColor=settings.Water;
            else if(altitude > .65f*settings.HeightScale) baseColor=settings.Snow;
            else if(altitude > .20f*settings.HeightScale || normal.Y < .65f) baseColor=settings.Rock;
            else baseColor=settings.Vegetation;
            Color lit = Color.FromArgb(
                Math.Clamp((int)(baseColor.R*brightness),0,255),
                Math.Clamp((int)(baseColor.G*brightness),0,255),
                Math.Clamp((int)(baseColor.B*brightness),0,255));
            faces.Add(new Face(projected[ax,az],projected[bx,bz],projected[cx,cz],
                (depth[ax,az]+depth[bx,bz]+depth[cx,cz])/3,lit));
        }
        void DrawAxis(Vector3 a, Vector3 b, Pen pen)
        {
            PointF Project(Vector3 p)
            {
                float rx=cosA*p.X-sinA*p.Z, rz=sinA*p.X+cosA*p.Z;
                return new PointF(width/2f+rx*scale,height*.58f-(cosE*p.Y-sinE*rz)*scale);
            }
            g.DrawLine(pen,Project(a),Project(b));
        }
    }
}
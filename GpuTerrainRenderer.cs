using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.GLControl;
using GL = OpenTK.Graphics.OpenGL4.GL;

namespace MountainGen.WinForms;

/// <summary>OpenGL 3.3 indexed terrain renderer. All methods must execute on the GLControl UI thread.</summary>
public sealed class GpuTerrainRenderer : IDisposable
{
    private int program, vao, vbo, ebo, indexCount;
    private int uMvp, uModel, uLight, uWater, uSnow, uRock, uVegetation, uWaterColour;
    private bool initialised;

    private const string VertexShader = """
        #version 330 core
        layout (location = 0) in vec3 aPosition;
        layout (location = 1) in vec3 aNormal;
        uniform mat4 uMvp;
        uniform mat4 uModel;
        out vec3 vNormal;
        out float vHeight;
        void main() {
            vec4 pos = uModel * vec4(aPosition, 1.0);
            vHeight = pos.y;
            vNormal = mat3(transpose(inverse(uModel))) * aNormal;
            gl_Position = uMvp * vec4(aPosition, 1.0);
        }
        """;

    private const string FragmentShader = """
        #version 330 core
        in vec3 vNormal;
        in float vHeight;
        uniform vec3 uLight;
        uniform vec3 uWaterColour;
        uniform vec3 uVegetation;
        uniform vec3 uRock;
        uniform vec3 uSnow;
        uniform float uWater;
        out vec4 FragColor;
        void main() {
            vec3 normal = normalize(vNormal);
            float slope = 1.0 - clamp(normal.y, 0.0, 1.0);
            vec3 colour = mix(uVegetation, uRock, smoothstep(0.1, 0.55, slope));
            colour = mix(colour, uRock, smoothstep(0.17, 0.38, vHeight));
            colour = mix(colour, uSnow, smoothstep(0.55, 0.75, vHeight));
            if (vHeight <= uWater + 0.0001) colour = uWaterColour;
            float diffuse = max(dot(normal, normalize(uLight)), 0.0);
            FragColor = vec4(colour * (0.32 + 0.68 * diffuse), 1.0);
        }
        """;

    public void Initialise()
    {
        if (initialised) return;
        int vertexShader = Compile(ShaderType.VertexShader, VertexShader);
        int fragmentShader = Compile(ShaderType.FragmentShader, FragmentShader);
        program = GL.CreateProgram();
        GL.AttachShader(program, vertexShader);
        GL.AttachShader(program, fragmentShader);
        GL.LinkProgram(program);
        GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int success);
        string log = GL.GetProgramInfoLog(program);
        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);
        if (success == 0) throw new InvalidOperationException("OpenGL program link: " + log);
        uMvp = GL.GetUniformLocation(program, "uMvp");
        uModel = GL.GetUniformLocation(program, "uModel");
        uLight = GL.GetUniformLocation(program, "uLight");
        uWater = GL.GetUniformLocation(program, "uWater");
        uSnow = GL.GetUniformLocation(program, "uSnow");
        uRock = GL.GetUniformLocation(program, "uRock");
        uVegetation = GL.GetUniformLocation(program, "uVegetation");
        uWaterColour = GL.GetUniformLocation(program, "uWaterColour");
        vao = GL.GenVertexArray();
        vbo = GL.GenBuffer();
        ebo = GL.GenBuffer();
        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Multisample);
        GL.Enable(EnableCap.CullFace);
        GL.CullFace(TriangleFace.Back);
        initialised = true;
    }

    private static int Compile(ShaderType type, string source)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
        if (success == 0)
        {
            string log = GL.GetShaderInfoLog(shader);
            GL.DeleteShader(shader);
            throw new InvalidOperationException("GLSL compile: " + log);
        }
        return shader;
    }

    public void Upload(Terrain terrain)
    {
        Initialise();
        int n = terrain.Size;
        // Interleaved position and normal (6 floats per vertex).
        var vertices = new float[n * n * 6];
        var indices = new uint[(n-1)*(n-1)*6];
        for (int z = 0; z < n; z++)
        for (int x = 0; x < n; x++)
        {
            float h = terrain.Heights[x,z];
            float left = terrain.Heights[Math.Max(0,x-1),z];
            float right = terrain.Heights[Math.Min(n-1,x+1),z];
            float down = terrain.Heights[x,Math.Max(0,z-1)];
            float up = terrain.Heights[x,Math.Min(n-1,z+1)];
            float gridStep = 2f/(n-1);
            var normal = Vector3.Normalize(new Vector3((left-right)/(2*gridStep), 1f, (down-up)/(2*gridStep)));
            int i = (z*n+x)*6;
            vertices[i] = x*gridStep-1;
            vertices[i+1] = h;
            vertices[i+2] = z*gridStep-1;
            vertices[i+3] = normal.X;
            vertices[i+4] = normal.Y;
            vertices[i+5] = normal.Z;
        }
        int k=0;
        for (int z=0;z<n-1;z++)
        for (int x=0;x<n-1;x++)
        {
            uint a=(uint)(z*n+x), b=a+1, c=a+(uint)n, d=c+1;
            // Counter-clockwise when viewed from above.
            indices[k++]=a; indices[k++]=c; indices[k++]=b;
            indices[k++]=b; indices[k++]=c; indices[k++]=d;
        }
        indexCount = indices.Length;
        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer,vbo);
        GL.BufferData(BufferTarget.ArrayBuffer,vertices.Length*sizeof(float),vertices,BufferUsageHint.StaticDraw);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer,ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer,indices.Length*sizeof(uint),indices,BufferUsageHint.StaticDraw);
        GL.VertexAttribPointer(0,3,VertexAttribPointerType.Float,false,6*sizeof(float),0);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1,3,VertexAttribPointerType.Float,false,6*sizeof(float),3*sizeof(float));
        GL.EnableVertexAttribArray(1);
        GL.BindVertexArray(0);
    }

    public void Render(GLControl surface, RenderSettings settings)
    {
        if (!initialised || surface.ClientSize.Width <= 0 || surface.ClientSize.Height <= 0) return;
        surface.MakeCurrent();
        GL.Viewport(0,0,surface.ClientSize.Width,surface.ClientSize.Height);
        GL.ClearColor(0.10f,0.17f,0.27f,1f);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        GL.UseProgram(program);
        var model = Matrix4.CreateScale(1f,settings.HeightScale,1f);
        float az = MathHelper.DegreesToRadians(settings.Azimuth);
        float el = MathHelper.DegreesToRadians(settings.Elevation);
        float distance = 3.7f / Math.Max(.2f, settings.Zoom);
        var eye = new Vector3(MathF.Sin(az)*MathF.Cos(el),MathF.Sin(el),MathF.Cos(az)*MathF.Cos(el))*distance;
        var view = Matrix4.LookAt(eye,Vector3.Zero,Vector3.UnitY);
        var projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(48f),
            Math.Max(.01f,(float)surface.ClientSize.Width/surface.ClientSize.Height),.05f,100f);
        // OpenTK uses row-vector matrices on the CPU; transpose when uploading to GLSL.
        var mvp = model*view*projection;
        GL.UniformMatrix4(uMvp,true,ref mvp);
        GL.UniformMatrix4(uModel,true,ref model);
        GL.Uniform3(uLight,-0.6f,1.2f,0.8f);
        GL.Uniform1(uWater,settings.ShowWater?settings.WaterLevel:-99f);
        SetColour(uSnow,settings.Snow);
        SetColour(uRock,settings.Rock);
        SetColour(uVegetation,settings.Vegetation);
        SetColour(uWaterColour,settings.Water);
        GL.PolygonMode(MaterialFace.FrontAndBack, settings.Wireframe?PolygonMode.Line:PolygonMode.Fill);
        GL.Disable(EnableCap.CullFace); // two-sided terrain; robust for wireframe and camera below horizon
        GL.BindVertexArray(vao);
        GL.DrawElements(PrimitiveType.Triangles,indexCount,DrawElementsType.UnsignedInt,0);
        GL.BindVertexArray(0);
        GL.PolygonMode(MaterialFace.FrontAndBack,PolygonMode.Fill);
        surface.SwapBuffers();
    }

    private static void SetColour(int location,System.Drawing.Color c)
        => GL.Uniform3(location,c.R/255f,c.G/255f,c.B/255f);

    public void Dispose()
    {
        if(!initialised)return;
        GL.DeleteBuffer(ebo);
        GL.DeleteBuffer(vbo);
        GL.DeleteVertexArray(vao);
        GL.DeleteProgram(program);
        initialised=false;
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class BladeMotionBlurTests
{
    public enum Swing
    {
        Still,
        Slow,
        Across,
        Fast,
        TowardCamera,
        Reverse,
        NearCamera,
        Diagonal,
        CrossesLiveAxis
    }

    private const int Size = 720;
    private const int BladeCenter = 288;
    private const int TestLayer = 30;
    private readonly List<Object> owned = new List<Object>();

    [SetUp]
    public void RequireGraphicsDevice()
    {
        Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null),
            "Run the render tests with graphics enabled.");
    }

    [TearDown]
    public void DestroyRenderObjects()
    {
        ClearRenderObjects();
    }

    [Test]
    public void SwingTowardCameraPreservesCenterFromRootToTip()
    {
        var still = Render(Swing.Still);
        var moving = Render(Swing.TowardCamera);
        float minimumCoverage = 1f;
        for (int y = 147; y < 622; y++)
        {
            float reference = Pixel(still, BladeCenter, y).r;
            Assert.That(reference, Is.GreaterThan(240f), "The reference blade must cover the center pixel.");
            minimumCoverage = Mathf.Min(minimumCoverage, Pixel(moving, BladeCenter, y).r / reference);
        }

        Assert.That(minimumCoverage, Is.GreaterThan(0.95f));
    }

    [TestCase(Swing.Slow, 45)]
    [TestCase(Swing.Across, 20)]
    [TestCase(Swing.Fast, 18)]
    [TestCase(Swing.Reverse, 26)]
    public void MovingFanFeathersBothEdges(Swing swing, int maxStep)
    {
        var pixels = Render(swing);
        foreach (int y in new[] { 225, 295, 400, 585 })
        {
            int maximumStep = 0;
            for (int x = 16; x < 490; x++)
                maximumStep = Mathf.Max(maximumStep, Mathf.Abs(Pixel(pixels, x, y).r - Pixel(pixels, x - 1, y).r));

            Assert.That(maximumStep, Is.LessThanOrEqualTo(maxStep), $"Sharp luminance change on row {y}");
            Assert.That(Pixel(pixels, BladeCenter, y).r, Is.GreaterThan(25), $"Gap next to the blade on row {y}");
        }
    }

    [TestCase(16, false)]
    [TestCase(5, false)]
    [TestCase(16, true)]
    public void FanWidensTowardTip(int sides, bool lit)
    {
        var pixels = Render(Swing.Across, sides, lit);
        int tip = VisibleWidth(pixels, 220);
        int middle = VisibleWidth(pixels, 360);
        int root = VisibleWidth(pixels, 520);

        Assert.That(root, Is.GreaterThan(20));
        Assert.That(middle, Is.GreaterThan(root));
        Assert.That(tip, Is.GreaterThan(middle));
    }

    [Test]
    public void ZeroSoftnessLeavesTheBladeInItsCurrentPose()
    {
        var still = Render(Swing.Still);
        var disabled = Render(Swing.Fast, softness: 0f);

        CollectionAssert.AreEqual(still, disabled);
    }

    [Test]
    public void HistoryCrossingNearPlaneKeepsFanWithinView()
    {
        var pixels = Render(Swing.NearCamera);

        Assert.That(VisibleWidth(pixels, 360), Is.InRange(60, 365));
        Assert.That(Pixel(pixels, 539, 360).r, Is.GreaterThan(35), "The fan must stay connected to the blade.");
        Assert.That(Pixel(pixels, 140, 360).r, Is.LessThan(8), "The fan reached the far side of the camera.");
    }

    [Test]
    public void DiagonalSwingDepositsLightOnlyWhereTheBladeSwept()
    {
        var pixels = Render(Swing.Diagonal);

        Assert.That(VisibleWidth(pixels, 250), Is.GreaterThan(65), "The upper blade should leave a lateral wake.");
        Assert.That(VisibleWidth(pixels, 585), Is.LessThan(50), "The older blade never reached the lower slice.");
        Assert.That(Pixel(pixels, 230, 585).r, Is.LessThan(8), "The fan extended below the older blade.");
    }

    [Test]
    public void ProjectionReversalKeepsTheMostRecentSide()
    {
        var pixels = Render(Swing.CrossesLiveAxis);

        Assert.That(Pixel(pixels, 325, 360).r, Is.GreaterThan(25), "The most recent wake should remain visible.");
        Assert.That(Pixel(pixels, 246, 360).r, Is.LessThan(8), "Older motion on the opposite side should be excluded.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OpaqueDarkPresetLayerClearsBloomUnderIt(bool lit)
    {
        var open = Render(Swing.Still, lit: lit);
        var masked = Render(Swing.Still, lit: lit, opaqueGlowMask: true);

        Assert.That(Pixel(open, BladeCenter, 360).a, Is.GreaterThan(60));
        Assert.That(Pixel(masked, BladeCenter, 360).a, Is.Zero);
    }

    private Color32[] Render(Swing swing, int sides = 16, bool lit = false, float softness = 1f,
                             bool opaqueGlowMask = false)
    {
        string shaderPath = lit ? "vs_blurpart_lit.shader" : "vs_blurpart.shader";
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/VainSabers/" + shaderPath);
        Assert.That(shader && shader.isSupported && !ShaderUtil.ShaderHasError(shader), Is.True, shaderPath);

        var target = Own(new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear));
        var camera = Own(new GameObject("Motion blur test camera")).AddComponent<Camera>();
        camera.transform.position = swing == Swing.NearCamera
            ? new Vector3(-0.1f, 0.55f, -0.62f)
            : new Vector3(0.14f, 0.55f, -2.15f);
        if (swing == Swing.NearCamera)
            camera.nearClipPlane = 0.06f;
        camera.fieldOfView = 36f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.015f, 0.02f, 0.028f, 0f);
        camera.targetTexture = target;
        camera.cullingMask = 1 << TestLayer;
        var gradient = Own(new Texture2D(128, 1, TextureFormat.RGBA32, false, true));
        gradient.wrapMode = TextureWrapMode.Clamp;
        for (int x = 0; x < gradient.width; x++)
            gradient.SetPixel(x, 0, new Color(1f - 0.68f * Mathf.SmoothStep(0f, 1f, x / 127f), 0f, 0f));
        gradient.Apply();

        // Opposite winding and different widths exercise layered presets.
        for (int layer = 0; layer < 2; layer++)
        {
            bool inner = layer > 0;
            var blade = Own(new GameObject("Motion blur test blade"));
            blade.layer = TestLayer;
            blade.AddComponent<MeshFilter>().sharedMesh = Own(Tube(sides, inner ? 0.009f : -0.018f));
            var material = Own(new Material(shader));
            blade.AddComponent<MeshRenderer>().sharedMaterial = material;
            material.renderQueue = 3100 + layer;
            material.SetFloat("_Glow", 0.75f);
            material.SetFloat("_VertexBlurFade", inner ? 2f : 5f);
            material.SetFloat("_VertexGlowMul", opaqueGlowMask && inner ? 0f : 1f);
            material.SetFloat("_VertexOpacityMul", inner ? 1f : 0.85f);
            material.SetFloat("_VainSaberBlurSoftness", softness);
            material.SetFloat("_VertexLength", 1f);
            material.SetVector("_VertexBladeAxis", new Vector4(0f, 1f, Mathf.Cos(Mathf.PI / sides), 0f));
            material.SetTexture("_OpacityMultiplierGradient", inner ? Texture2D.whiteTexture : gradient);
            material.SetTexture("_GlowAddendGradient", Texture2D.blackTexture);
            material.SetTexture("_RimPowerGradient", Texture2D.blackTexture);
            material.SetVector("_VertexCustomColor", inner ? new Vector4(1f, 0.25f, 0.08f, 1f) : new Vector4(1f, 0.045f, 0.03f, 1f));
            SetHistory(material, swing);
        }

        var previousTarget = RenderTexture.active;
        try
        {
            camera.Render();
            RenderTexture.active = target;
            var image = Own(new Texture2D(Size, Size, TextureFormat.RGBA32, false, true));
            image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            return image.GetPixels32();
        }
        finally
        {
            RenderTexture.active = previousTarget;
            ClearRenderObjects();
        }
    }

    private static void SetHistory(Material material, Swing swing)
    {
        var positions = new Vector4[8];
        var forwards = new Vector4[8];
        var ups = new Vector4[8];
        for (int i = 0; i < positions.Length; i++)
        {
            float age = i / (float)(positions.Length - 1);
            float sweep = swing == Swing.Reverse
                ? Mathf.Min(age, 0.57f) - 0.9f * Mathf.Max(0f, age - 0.57f)
                : age;
            var rotation = swing switch
            {
                Swing.Still or Swing.NearCamera or Swing.Diagonal or Swing.CrossesLiveAxis => Quaternion.identity,
                Swing.TowardCamera => Quaternion.AngleAxis(age * 37f, Vector3.right),
                Swing.Slow => Quaternion.AngleAxis(sweep * 0.033f * Mathf.Rad2Deg, Vector3.forward),
                Swing.Fast => Quaternion.AngleAxis(sweep * 0.68f * Mathf.Rad2Deg, Vector3.forward),
                _ => Quaternion.AngleAxis(sweep * 0.43f * Mathf.Rad2Deg, Vector3.forward)
            };
            if (swing == Swing.TowardCamera)
                positions[i] = new Vector4(0f, -0.08f * age, 0.22f * age);
            else if (swing == Swing.NearCamera)
                positions[i] = new Vector4(-0.21f * age, 0f, -0.565f * age);
            else if (swing == Swing.Diagonal)
                positions[i] = new Vector4(-0.18f * age, 0.65f * age, -0.10f * age);
            else if (swing == Swing.CrossesLiveAxis)
                positions[i].x = 0.32f * Mathf.Min(0.5f, age) - 0.9f * Mathf.Max(0f, age - 0.5f);
            forwards[i] = rotation * Vector3.up;
            ups[i] = rotation * Vector3.back;
        }
        material.SetVectorArray("_HistPos", positions);
        material.SetVectorArray("_HistFwd", forwards);
        material.SetVectorArray("_HistUp", ups);
    }

    private static Mesh Tube(int sides, float size)
    {
        const int rows = 30;
        var positions = new List<Vector3>();
        var polar = new List<Vector3>();
        var meta = new List<Vector4>();
        var color = new List<Color>();
        var data = new List<Vector4>();
        var indices = new List<int>();
        for (int row = 0; row <= rows; row++)
        {
            float along = row / (float)rows;
            float radius = Mathf.Abs(size) * Mathf.Lerp(1f, 0.33f, along);
            bool cap = row == 0 || row == rows;
            for (int col = 0; col <= sides; col++)
            {
                float theta = 2f * Mathf.PI * col / sides;
                positions.Add(new Vector3(along, 0f, 0f));
                polar.Add(new Vector3(Mathf.Cos(theta), Mathf.Sin(theta), Mathf.Sign(size)));
                meta.Add(new Vector4(0f, cap ? 1f : 0f, along, radius));
                color.Add(Color.white);
                data.Add(new Vector4(1f, 1f, 0f, 0f));
                if (row == rows || col == sides)
                    continue;
                int a = row * (sides + 1) + col;
                int c = a + sides + 1;
                indices.AddRange(new[] { a, c, a + 1, a + 1, c, c + 1 });
            }
        }
        var mesh = new Mesh();
        mesh.SetVertices(positions);
        mesh.SetNormals(polar);
        mesh.SetTangents(meta);
        mesh.SetColors(color);
        mesh.SetUVs(1, data);
        mesh.SetTriangles(indices, 0);
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 20f);
        return mesh;
    }

    private static Color32 Pixel(Color32[] pixels, int x, int y) => pixels[(Size - 1 - y) * Size + x];

    private static int VisibleWidth(Color32[] pixels, int y)
    {
        int count = 0;
        for (int x = 0; x < Size; x++)
            if (Pixel(pixels, x, y).r >= 21)
                count++;
        return count;
    }

    private T Own<T>(T obj) where T : Object
    {
        owned.Add(obj);
        return obj;
    }

    private void ClearRenderObjects()
    {
        for (int i = owned.Count - 1; i >= 0; i--)
            Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }
}

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SimPlanet;

/// <summary>
/// Top-down vehicle sprites (24 px, pointing east so they can be rotated by heading).
/// Each comes with a mask drawn in the operator's nation colour.
/// </summary>
public partial class MapIcons
{
    private Texture2D[]? _vehicleBase;
    private Texture2D[]? _vehicleMask;

    public (Texture2D Base, Texture2D Mask)? VehicleSprite(VehicleKind kind)
    {
        int i = (int)kind;
        if (_vehicleBase == null || _vehicleMask == null || i < 0 || i >= _vehicleBase.Length) return null;
        return (_vehicleBase[i], _vehicleMask[i]);
    }

    /// <summary>Spy networks view icon.</summary>
    public Texture2D? Eye { get; private set; }

    /// <summary>Migrations view icon: people walking along an arrow.</summary>
    public Texture2D? Migration { get; private set; }

    /// <summary>Wildlife overlay icon: a small herd.</summary>
    public Texture2D? Herd { get; private set; }

    private void BuildVehicles(GraphicsDevice device)
    {
        Eye = BuildEye(device);
        Migration = BuildMigration(device);
        Herd = BuildHerd(device);
        var kinds = Enum.GetValues<VehicleKind>();
        _vehicleBase = new Texture2D[kinds.Length];
        _vehicleMask = new Texture2D[kinds.Length];
        foreach (var k in kinds)
        {
            var (b, m) = BuildVehicle(device, k);
            _vehicleBase[(int)k] = b;
            _vehicleMask[(int)k] = m;
        }
    }

    private void DisposeVehicles()
    {
        Eye?.Dispose();
        Migration?.Dispose();
        Herd?.Dispose();
        if (_vehicleBase != null) foreach (var t in _vehicleBase) t?.Dispose();
        if (_vehicleMask != null) foreach (var t in _vehicleMask) t?.Dispose();
    }

    private static (Texture2D, Texture2D) BuildVehicle(GraphicsDevice device, VehicleKind kind)
    {
        var b = new Canvas(SmallIconSize);
        var m = new Canvas(SmallIconSize);
        var tintBase = new Color(128, 128, 128);
        switch (kind)
        {
            case VehicleKind.Caravan:
            {
                // Covered wagon pulled by two oxen
                b.Rect(2, 8, 10, 8, new Color(236, 224, 196));
                for (float x = 4; x < 12; x += 2.6f) b.Rect(x, 8, 0.6f, 8, new Color(190, 170, 130));
                b.Rect(2.5f, 6.8f, 2.4f, 1.4f, new Color(60, 44, 30));
                b.Rect(8.5f, 6.8f, 2.4f, 1.4f, new Color(60, 44, 30));
                b.Rect(2.5f, 15.8f, 2.4f, 1.4f, new Color(60, 44, 30));
                b.Rect(8.5f, 15.8f, 2.4f, 1.4f, new Color(60, 44, 30));
                b.Line(V(12, 12), V(15, 12), 0.8f, new Color(100, 70, 40));
                var ox = new Color(128, 88, 56);
                b.Circle(17, 9.5f, 2.2f, ox); b.Circle(19.8f, 9.5f, 1.3f, ox);
                b.Circle(17, 14.5f, 2.2f, ox); b.Circle(19.8f, 14.5f, 1.3f, ox);
                b.Rect(2, 11, 10, 2, tintBase);
                m.Rect(2, 11, 10, 2, MaskLight);
                break;
            }
            case VehicleKind.Truck:
            {
                b.Rect(2, 8, 13, 8, new Color(226, 228, 232));
                b.Rect(2, 8, 13, 1.2f, new Color(180, 184, 190));
                b.Rect(15.6f, 8.6f, 6, 6.8f, tintBase);
                m.Rect(15.6f, 8.6f, 6, 6.8f, MaskLight);
                b.Rect(19.6f, 9.4f, 1.6f, 5.2f, new Color(130, 180, 220));
                b.Rect(3, 11, 11, 2, tintBase);
                m.Rect(3, 11, 11, 2, new Color(255, 255, 255, 170));
                break;
            }
            case VehicleKind.Train:
            {
                var car = new Color(150, 110, 80);
                b.Rect(0.5f, 9, 6, 6, car);
                b.Rect(7.5f, 9, 6, 6, car);
                b.Rect(1, 9.6f, 5, 1, new Color(190, 150, 110));
                b.Rect(8, 9.6f, 5, 1, new Color(190, 150, 110));
                b.Rect(6.4f, 11.5f, 1.2f, 1, new Color(40, 40, 40));
                b.Rect(13.4f, 11.5f, 1.2f, 1, new Color(40, 40, 40));
                b.Poly(tintBase, V(14.5f, 8.6f), V(21, 8.6f), V(23, 12), V(21, 15.4f), V(14.5f, 15.4f));
                m.Poly(MaskLight, V(14.5f, 8.6f), V(21, 8.6f), V(23, 12), V(21, 15.4f), V(14.5f, 15.4f));
                b.Rect(15.5f, 10.8f, 4, 2.4f, new Color(50, 50, 56));
                break;
            }
            case VehicleKind.SailingShip:
            {
                var hull = new[] { V(2, 9), V(16, 8.6f), V(22.5f, 12), V(16, 15.4f), V(2, 15) };
                b.Poly(new Color(140, 96, 58), hull);
                b.Poly(new Color(186, 140, 90), V(3.5f, 10.2f), V(15.5f, 10), V(20, 12), V(15.5f, 14), V(3.5f, 13.8f));
                // Sails seen from above: yards across the hull with billowing canvas
                var sail = new Color(246, 242, 228);
                b.Poly(sail, V(6.5f, 4.5f), V(8.6f, 4.8f), V(9.4f, 12), V(8.6f, 19.2f), V(6.5f, 19.5f));
                b.Poly(sail, V(12.5f, 5.5f), V(14.4f, 5.8f), V(15.2f, 12), V(14.4f, 18.2f), V(12.5f, 18.5f));
                b.Poly(tintBase, V(15.8f, 12), V(20, 11.2f), V(20, 12.8f));
                m.Poly(MaskLight, V(15.8f, 12), V(20, 11.2f), V(20, 12.8f));
                break;
            }
            case VehicleKind.Steamship:
            {
                var hull = new[] { V(2, 8.5f), V(17, 8.2f), V(22.5f, 12), V(17, 15.8f), V(2, 15.5f) };
                b.Poly(new Color(46, 48, 56), hull);
                b.Poly(new Color(170, 150, 120), V(3.5f, 9.8f), V(16.5f, 9.6f), V(20, 12), V(16.5f, 14.4f), V(3.5f, 14.2f));
                b.Rect(6, 10, 6, 4, new Color(236, 236, 230));
                b.Circle(9.5f, 12, 1.9f, tintBase);
                m.Circle(9.5f, 12, 1.9f, MaskLight);
                b.Circle(9.5f, 12, 0.9f, new Color(30, 30, 30));
                break;
            }
            case VehicleKind.CargoShip:
            {
                var hull = new[] { V(1, 8), V(19, 8), V(23, 12), V(19, 16), V(1, 16) };
                b.Poly(new Color(120, 40, 36), hull);
                b.Rect(2, 9, 3.5f, 6, new Color(236, 236, 236));
                Color[] boxes = { new Color(210, 80, 60), new Color(70, 120, 200), new Color(80, 160, 90), new Color(230, 190, 70) };
                for (int i = 0; i < 4; i++)
                {
                    float x = 6.5f + i * 3.2f;
                    b.Rect(x, 9, 2.8f, 2.8f, boxes[i % 4]);
                    b.Rect(x, 12.2f, 2.8f, 2.8f, boxes[(i + 2) % 4]);
                }
                b.Rect(6.5f, 11.6f, 12.4f, 0.8f, tintBase);
                m.Rect(6.5f, 11.6f, 12.4f, 0.8f, MaskLight);
                m.Rect(2, 9, 3.5f, 6, new Color(255, 255, 255, 90));
                break;
            }
            case VehicleKind.Airliner:
            {
                var white = new Color(244, 246, 250);
                b.Line(V(2.5f, 12), V(21.5f, 12), 2.6f, white);
                b.Circle(21.5f, 12, 1.3f, white);
                b.Poly(white, V(10, 12), V(15, 12), V(9.5f, 1.5f), V(7.8f, 1.5f));
                b.Poly(white, V(10, 12), V(15, 12), V(9.5f, 22.5f), V(7.8f, 22.5f));
                b.Poly(white, V(2.5f, 12), V(5.5f, 12), V(3, 7), V(1.8f, 7));
                b.Poly(white, V(2.5f, 12), V(5.5f, 12), V(3, 17), V(1.8f, 17));
                b.Line(V(3, 12), V(6, 12), 1.2f, tintBase);
                m.Line(V(3, 12), V(6, 12), 1.2f, MaskLight);
                b.Rect(20, 11.4f, 1.2f, 1.2f, new Color(60, 80, 110));
                break;
            }
            default: // Warship
            {
                var hull = new[] { V(1.5f, 9.4f), V(16, 8.2f), V(23, 12), V(16, 15.8f), V(1.5f, 14.6f) };
                b.Poly(new Color(120, 128, 138), hull);
                b.Rect(8, 10, 5, 4, new Color(160, 168, 178));
                b.Circle(17, 12, 1.8f, new Color(90, 96, 106));
                b.Line(V(17, 12), V(21, 12), 0.8f, new Color(70, 74, 82));
                b.Circle(5, 12, 1.8f, new Color(90, 96, 106));
                b.Line(V(5, 12), V(1.5f, 12), 0.8f, new Color(70, 74, 82));
                b.Rect(9, 11.2f, 3, 1.6f, tintBase);
                m.Rect(9, 11.2f, 3, 1.6f, MaskLight);
                break;
            }
        }
        b.Outline(0.8f, Outline);
        return (b.ToTexture(device), m.ToTexture(device));
    }

    private static Texture2D BuildEye(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        c.Poly(new Color(236, 240, 246), V(2, 12), V(7, 6.5f), V(12, 5), V(17, 6.5f), V(22, 12), V(17, 17.5f), V(12, 19), V(7, 17.5f));
        c.Circle(12, 12, 5, new Color(90, 150, 220));
        c.Circle(12, 12, 2.4f, new Color(20, 22, 30));
        c.Circle(10.6f, 10.6f, 1f, Color.White);
        c.Outline(1.0f, Outline);
        return c.ToTexture(device);
    }

    private static Texture2D BuildMigration(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        var arrow = new Color(90, 215, 200);
        c.Line(V(3, 19), V(18, 19), 2.2f, arrow);
        c.Poly(arrow, V(17, 15.5f), V(22.5f, 19), V(17, 22.5f));
        var body = new Color(240, 220, 180);
        foreach (var x in new[] { 6f, 13f })
        {
            c.Circle(x, 4.5f, 2f, body);
            c.Line(V(x, 6.5f), V(x, 11.5f), 2.2f, body);
            c.Line(V(x, 11.5f), V(x - 2.2f, 16), 1.6f, body);
            c.Line(V(x, 11.5f), V(x + 2.4f, 16), 1.6f, body);
            c.Line(V(x, 8), V(x + 2.6f, 10.5f), 1.4f, body);
        }
        c.Rect(15.5f, 6, 3.5f, 4.5f, new Color(170, 120, 70)); // bundle
        c.Outline(1.0f, Outline);
        return c.ToTexture(device);
    }

    private static Texture2D BuildHerd(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        var hide = new Color(150, 104, 66);
        foreach (var (x, y) in new (float, float)[] { (7, 15), (15, 12), (13, 19) })
        {
            c.Circle(x, y, 3.4f, hide);
            c.Circle(x + 3.6f, y - 1.6f, 1.8f, hide);
            c.Rect(x - 2.4f, y + 2, 0.9f, 2.6f, hide);
            c.Rect(x + 1.6f, y + 2, 0.9f, 2.6f, hide);
        }
        c.Line(V(3, 6), V(5, 4), 1f, new Color(30, 30, 36));
        c.Line(V(5, 4), V(7, 6), 1f, new Color(30, 30, 36));
        c.Line(V(9, 4), V(11, 2), 1f, new Color(30, 30, 36));
        c.Line(V(11, 2), V(13, 4), 1f, new Color(30, 30, 36));
        c.Outline(0.9f, Outline);
        return c.ToTexture(device);
    }
}

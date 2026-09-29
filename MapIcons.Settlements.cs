using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SimPlanet;

/// <summary>
/// Settlement sprites for each architectural tradition (<see cref="CityStyle"/>) and size tier,
/// plus the small specialization badges (harbour, mine, market, university...).
///
/// Roofs keep their material colour (thatch, red tiles, lacquer...) and are only partly
/// tinted by the nation colour through a semi-transparent mask; banners are fully tinted.
/// </summary>
public partial class MapIcons
{
    public static readonly CityStyle[] AllStyles = Enum.GetValues<CityStyle>();
    public static readonly CitySpecialization[] AllSpecializations = Enum.GetValues<CitySpecialization>();

    private Texture2D[,]? _styledBase;
    private Texture2D[,]? _styledMask;
    private Texture2D[]? _badges;

    // Mask shades: roofs let the material show through; banners are pure nation colour
    private static readonly Color RoofMask = new Color(255, 255, 255, 150);
    private static readonly Color RoofMaskShade = new Color(180, 180, 180, 150);
    private static readonly Color TrimMask = new Color(255, 255, 255, 110);

    /// <summary>Settlement sprite (base, nation-colour mask) for a style and size tier.</summary>
    public (Texture2D Base, Texture2D Mask) Settlement(CityStyle style, int type)
    {
        int s = Math.Clamp((int)style, 0, AllStyles.Length - 1);
        int t = Math.Clamp(type, 0, 3);
        if (_styledBase == null || _styledMask == null) return (SettlementBase[t], SettlementMask[t]);
        return (_styledBase[s, t], _styledMask[s, t]);
    }

    /// <summary>Small badge for a settlement specialization (null for none).</summary>
    public Texture2D? SpecializationBadge(CitySpecialization spec)
    {
        int i = (int)spec;
        if (_badges == null || i < 0 || i >= _badges.Length) return null;
        return _badges[i];
    }

    private void BuildStyledSettlements(GraphicsDevice device)
    {
        _styledBase = new Texture2D[AllStyles.Length, 4];
        _styledMask = new Texture2D[AllStyles.Length, 4];
        foreach (var style in AllStyles)
        {
            for (int t = 0; t < 4; t++)
            {
                var (b, m) = BuildStyled(device, style, t);
                _styledBase[(int)style, t] = b;
                _styledMask[(int)style, t] = m;
            }
        }

        _badges = new Texture2D[AllSpecializations.Length];
        foreach (var spec in AllSpecializations)
            _badges[(int)spec] = BuildBadge(device, spec);
    }

    private void DisposeStyled()
    {
        if (_styledBase != null) foreach (var t in _styledBase) t?.Dispose();
        if (_styledMask != null) foreach (var t in _styledMask) t?.Dispose();
        if (_badges != null) foreach (var t in _badges) t?.Dispose();
    }

    private static (Texture2D, Texture2D) BuildStyled(GraphicsDevice device, CityStyle style, int type)
    {
        var b = new Canvas(SettlementSize);
        var m = new Canvas(SettlementSize);
        float g = SettlementSize - 6;
        switch (style)
        {
            case CityStyle.Timber: Timber(b, m, type, g); break;
            case CityStyle.Stone: StoneTown(b, m, type, g); break;
            case CityStyle.Adobe: Adobe(b, m, type, g); break;
            case CityStyle.Stilt: Stilt(b, m, type, g); break;
            case CityStyle.Pagoda: Pagoda(b, m, type, g); break;
            case CityStyle.Terraced: Terraced(b, m, type, g); break;
            case CityStyle.Modern: Modern(b, m, type, g); break;
            default: Futuristic(b, m, type, g); break;
        }
        b.Outline(1.1f, Outline);
        return (b.ToTexture(device), m.ToTexture(device));
    }

    // ------------------------------------------------------------------
    // Shared pieces
    // ------------------------------------------------------------------

    private static void Banner(Canvas b, Canvas m, float x, float top, float len = 7f)
    {
        b.Rect(x, top, 1.1f, 8.5f, new Color(60, 48, 38));
        var flag = new[] { V(x + 1.1f, top), V(x + 1.1f + len, top + 1.8f), V(x + 1.1f, top + 3.8f) };
        b.Poly(new Color(128, 128, 128), flag);
        m.Poly(MaskLight, flag);
    }

    private static void TintedRoof(Canvas b, Canvas m, Color roof, Color shade, Vector2[] pts, Vector2[]? shadePts = null)
    {
        b.Poly(roof, pts);
        m.Poly(RoofMask, pts);
        if (shadePts != null)
        {
            b.Poly(shade, shadePts);
            m.Poly(RoofMaskShade, shadePts);
        }
    }

    private static void Ground(Canvas b, float x0, float x1, float g, Color c) => b.Rect(x0, g - 1, x1 - x0, 3.5f, c);

    // ------------------------------------------------------------------
    // Timber: wooden halls, steep shingle roofs, crossed gables, stave churches
    // ------------------------------------------------------------------

    private static readonly Color TimberWall = new Color(156, 108, 62);
    private static readonly Color TimberShade = new Color(120, 80, 46);
    private static readonly Color TimberPlank = new Color(96, 64, 38);
    private static readonly Color Shingle = new Color(92, 70, 56);
    private static readonly Color ShingleShade = new Color(66, 50, 42);

    private static void TimberHouse(Canvas b, Canvas m, float x, float g, float w, float h)
    {
        b.Rect(x, g - h, w, h, TimberWall);
        b.Rect(x + w * 0.62f, g - h, w * 0.38f, h, TimberShade);
        for (float px = x + 2f; px < x + w - 0.5f; px += 2.4f) b.Rect(px, g - h, 0.45f, h, TimberPlank);
        b.Rect(x + w * 0.4f, g - h * 0.6f, w * 0.22f, h * 0.6f, Door);
        float roofH = w * 0.85f;
        var apex = V(x + w / 2f, g - h - roofH);
        TintedRoof(b, m, Shingle, ShingleShade,
            new[] { V(x - 1.3f, g - h + 0.4f), apex, V(x + w + 1.3f, g - h + 0.4f) },
            new[] { apex, V(x + w + 1.3f, g - h + 0.4f), V(x + w / 2f, g - h + 0.4f) });
        // Crossed gable boards
        b.Line(apex, apex + V(-2.2f, -2.6f), 0.9f, TimberWall);
        b.Line(apex, apex + V(2.2f, -2.6f), 0.9f, TimberWall);
    }

    private static void StaveChurch(Canvas b, Canvas m, float cx, float g, float scale)
    {
        float w1 = 11 * scale, h1 = 7 * scale;
        b.Rect(cx - w1 / 2, g - h1, w1, h1, TimberWall);
        b.Rect(cx + w1 * 0.1f, g - h1, w1 * 0.4f, h1, TimberShade);
        b.Rect(cx - 1.2f, g - h1 * 0.6f, 2.4f, h1 * 0.6f, Door);
        float y1 = g - h1;
        TintedRoof(b, m, Shingle, ShingleShade, new[] { V(cx - w1 / 2 - 1.5f, y1 + 0.4f), V(cx, y1 - 5 * scale), V(cx + w1 / 2 + 1.5f, y1 + 0.4f) });
        float w2 = 6.5f * scale, y2 = y1 - 3.5f * scale;
        b.Rect(cx - w2 / 2, y2 - 3.5f * scale, w2, 4.5f * scale, TimberWall);
        TintedRoof(b, m, Shingle, ShingleShade, new[] { V(cx - w2 / 2 - 1.2f, y2 - 3.2f * scale), V(cx, y2 - 9f * scale), V(cx + w2 / 2 + 1.2f, y2 - 3.2f * scale) });
        float top = y2 - 9f * scale;
        b.Line(V(cx, top), V(cx, top - 4 * scale), 0.9f, new Color(70, 52, 40));
        b.Line(V(cx, top + 1), V(cx - 2.5f, top - 1.5f), 0.8f, TimberWall);
        b.Line(V(cx, top + 1), V(cx + 2.5f, top - 1.5f), 0.8f, TimberWall);
    }

    private static void Palisade(Canvas b, float x0, float x1, float g)
    {
        for (float x = x0; x < x1; x += 2.1f)
        {
            b.Poly(new Color(128, 90, 54), V(x, g + 1.5f), V(x, g - 5), V(x + 0.9f, g - 6.5f), V(x + 1.8f, g - 5), V(x + 1.8f, g + 1.5f));
        }
        b.Rect(x0, g - 3.5f, x1 - x0, 0.9f, new Color(90, 62, 36));
    }

    private static void Timber(Canvas b, Canvas m, int type, float g)
    {
        switch (type)
        {
            case 0:
                Ground(b, 3, 37, g, new Color(96, 120, 64));
                TimberHouse(b, m, 5, g, 12, 9);
                TimberHouse(b, m, 21, g + 1, 13, 8);
                b.Rect(18, g - 3, 2.2f, 3.5f, new Color(120, 86, 50)); // woodpile
                break;
            case 1:
                Ground(b, 2, 38, g, new Color(96, 120, 64));
                TimberHouse(b, m, 2, g + 1, 10, 8);
                StaveChurch(b, m, 20, g, 1f);
                TimberHouse(b, m, 28, g + 1, 10, 9);
                break;
            case 2:
                TimberHouse(b, m, 1, g - 3, 9, 8);
                TimberHouse(b, m, 30, g - 3, 9, 8);
                StaveChurch(b, m, 20, g - 2, 1.2f);
                // Great hall in front
                TimberHouse(b, m, 9, g, 7, 6);
                Palisade(b, 1, 39, g + 1);
                Banner(b, m, 34, 4);
                break;
            default:
                TimberHouse(b, m, 0.5f, g - 5, 8, 9);
                TimberHouse(b, m, 31, g - 6, 8, 11);
                StaveChurch(b, m, 20, g - 2, 1.2f);
                TimberHouse(b, m, 6, g, 9, 7);
                TimberHouse(b, m, 25, g, 9, 7);
                Palisade(b, 0.5f, 39.5f, g + 1.5f);
                Banner(b, m, 3, 8);
                break;
        }
    }

    // ------------------------------------------------------------------
    // Stone: masonry walls, red tiled roofs, bell towers and keeps
    // ------------------------------------------------------------------

    private static readonly Color StoneWall = new Color(218, 206, 180);
    private static readonly Color StoneWallShade = new Color(178, 164, 140);
    private static readonly Color Tile = new Color(194, 86, 56);
    private static readonly Color TileShade = new Color(150, 62, 42);

    private static void StoneHouse(Canvas b, Canvas m, float x, float g, float w, float h, float roofH, bool window = true)
    {
        b.Rect(x, g - h, w, h, StoneWall);
        b.Rect(x + w * 0.62f, g - h, w * 0.38f, h, StoneWallShade);
        if (window && w >= 5)
        {
            b.Rect(x + w * 0.18f, g - h * 0.7f, w * 0.22f, h * 0.3f, Window);
            b.Rect(x + w * 0.56f, g - h * 0.55f, w * 0.2f, h * 0.55f, Door);
        }
        var apex = V(x + w / 2f, g - h - roofH);
        TintedRoof(b, m, Tile, TileShade,
            new[] { V(x - 1f, g - h + 0.3f), apex, V(x + w + 1f, g - h + 0.3f) },
            new[] { apex, V(x + w + 1f, g - h + 0.3f), V(x + w / 2f, g - h + 0.3f) });
    }

    private static void BellTower(Canvas b, Canvas m, float x, float g, float w, float h)
    {
        b.Rect(x, g - h, w, h, Stone);
        b.Rect(x + w * 0.55f, g - h, w * 0.45f, h, StoneDark);
        b.Rect(x + w * 0.3f, g - h * 0.75f, w * 0.38f, h * 0.2f, new Color(50, 44, 40));
        TintedRoof(b, m, Tile, TileShade,
            new[] { V(x - 1f, g - h + 0.5f), V(x + w / 2f, g - h - w * 1.2f), V(x + w + 1f, g - h + 0.5f) },
            new[] { V(x + w / 2f, g - h - w * 1.2f), V(x + w + 1f, g - h + 0.5f), V(x + w / 2f, g - h + 0.5f) });
    }

    private static void StoneTown(Canvas b, Canvas m, int type, float g)
    {
        switch (type)
        {
            case 0:
                Ground(b, 3, 37, g, new Color(150, 120, 70));
                StoneHouse(b, m, 4, g, 15, 11, 7);
                StoneHouse(b, m, 22, g + 1, 13, 9, 6);
                break;
            case 1:
                Ground(b, 2, 38, g, new Color(150, 130, 100));
                StoneHouse(b, m, 2, g + 1, 12, 10, 6);
                BellTower(b, m, 15.5f, g, 8, 21);
                StoneHouse(b, m, 25, g + 1, 13, 11, 6);
                break;
            case 2:
                StoneHouse(b, m, 2, g - 2, 9, 10, 5, false);
                StoneHouse(b, m, 28, g - 3, 10, 11, 5, false);
                // Keep with crenellations
                b.Rect(13, g - 22, 14, 22, Stone);
                b.Rect(21, g - 22, 6, 22, StoneDark);
                for (int i = 0; i < 4; i++) b.Rect(13 + i * 4, g - 25, 2.4f, 3, Stone);
                b.Rect(16, g - 17, 2.5f, 4, Window);
                b.Rect(22, g - 17, 2.5f, 4, Window);
                Banner(b, m, 19.6f, g - 33);
                b.Rect(1, g - 5, 38, 6, Stone);
                b.Rect(1, g - 2, 38, 3, StoneDark);
                for (int i = 0; i < 10; i++) b.Rect(1.5f + i * 3.8f, g - 7, 2, 2, Stone);
                break;
            default:
                // Old town under a cathedral: tiled roofs packed around twin towers
                BellTower(b, m, 11, g - 2, 6, 26);
                BellTower(b, m, 23, g - 2, 6, 26);
                b.Rect(17, g - 18, 6, 16, StoneWall);
                b.Circle(20, g - 12, 1.9f, new Color(120, 160, 220));
                StoneHouse(b, m, 0.5f, g, 9, 12, 5, false);
                StoneHouse(b, m, 30.5f, g, 9, 13, 5, false);
                StoneHouse(b, m, 8, g + 1, 8, 8, 4);
                StoneHouse(b, m, 23, g + 1, 8, 8, 4);
                b.Rect(0, g + 1, 40, 2, new Color(130, 120, 110));
                break;
        }
    }

    // ------------------------------------------------------------------
    // Adobe: mud brick cubes, flat roofs with beams, tiled domes and minarets
    // ------------------------------------------------------------------

    private static readonly Color AdobeWall = new Color(216, 170, 112);
    private static readonly Color AdobeShade = new Color(182, 136, 86);
    private static readonly Color AdobeDark = new Color(84, 58, 40);
    private static readonly Color DomeTile = new Color(70, 150, 170);
    private static readonly Color DomeShade = new Color(48, 110, 130);

    private static void AdobeHouse(Canvas b, Canvas m, float x, float g, float w, float h)
    {
        b.Rect(x, g - h, w, h, AdobeWall);
        b.Rect(x + w * 0.64f, g - h, w * 0.36f, h, AdobeShade);
        // Parapet and protruding beams (vigas)
        b.Rect(x - 0.4f, g - h - 1.2f, w + 0.8f, 1.6f, AdobeShade);
        m.Rect(x - 0.4f, g - h - 1.2f, w + 0.8f, 1.6f, TrimMask);
        for (float bx = x + 1.2f; bx < x + w - 0.5f; bx += 2.6f) b.Rect(bx, g - h + 1.2f, 0.9f, 0.9f, AdobeDark);
        b.Rect(x + w * 0.2f, g - h * 0.62f, w * 0.18f, h * 0.22f, AdobeDark);
        // Arched door with a tinted awning
        b.Rect(x + w * 0.52f, g - h * 0.5f, w * 0.22f, h * 0.5f, AdobeDark);
        b.Circle(x + w * 0.63f, g - h * 0.5f, w * 0.11f, AdobeDark);
    }

    private static void DomeHall(Canvas b, Canvas m, float cx, float g, float w, float h)
    {
        float r = w * 0.42f;
        var domeTop = g - h;
        b.Circle(cx, domeTop, r, DomeTile);
        m.Circle(cx, domeTop, r, RoofMask);
        b.Poly(DomeShade, V(cx, domeTop - r), V(cx + r, domeTop), V(cx, domeTop));
        b.Rect(cx - 0.4f, domeTop - r - 3f, 0.9f, 3f, new Color(230, 200, 90));
        b.Circle(cx, domeTop - r - 3.2f, 0.9f, new Color(240, 210, 100));
        b.Rect(cx - w / 2, domeTop, w, h, AdobeWall);
        b.Rect(cx + w * 0.14f, domeTop, w * 0.36f, h, AdobeShade);
        b.Rect(cx - 1.4f, g - h * 0.55f, 2.8f, h * 0.55f, AdobeDark);
        b.Circle(cx, g - h * 0.55f, 1.4f, AdobeDark);
    }

    private static void Minaret(Canvas b, Canvas m, float x, float g, float h)
    {
        b.Rect(x, g - h, 3.2f, h, AdobeWall);
        b.Rect(x + 1.9f, g - h, 1.3f, h, AdobeShade);
        b.Rect(x - 0.8f, g - h * 0.72f, 4.8f, 1.3f, AdobeShade);
        var cap = new[] { V(x - 0.3f, g - h + 0.2f), V(x + 1.6f, g - h - 4f), V(x + 3.5f, g - h + 0.2f) };
        b.Poly(DomeTile, cap);
        m.Poly(RoofMask, cap);
    }

    private static void Adobe(Canvas b, Canvas m, int type, float g)
    {
        switch (type)
        {
            case 0:
                Ground(b, 3, 37, g, new Color(196, 160, 100));
                AdobeHouse(b, m, 5, g, 13, 10);
                AdobeHouse(b, m, 21, g + 1, 12, 8);
                b.Circle(35, g - 3, 2.6f, new Color(90, 140, 70)); // palm crown
                b.Rect(34.6f, g - 3, 0.9f, 4, new Color(110, 80, 50));
                break;
            case 1:
                Ground(b, 2, 38, g, new Color(196, 160, 100));
                AdobeHouse(b, m, 2, g + 1, 10, 9);
                DomeHall(b, m, 20, g, 13, 10);
                AdobeHouse(b, m, 28, g + 1, 10, 11);
                break;
            case 2:
                AdobeHouse(b, m, 1, g - 3, 9, 10);
                AdobeHouse(b, m, 30, g - 3, 9, 11);
                DomeHall(b, m, 19, g - 2, 16, 11);
                Minaret(b, m, 29, g - 3, 25);
                // Mud brick wall with rounded merlons
                b.Rect(1, g - 5, 38, 6, AdobeWall);
                b.Rect(1, g - 2, 38, 3, AdobeShade);
                for (int i = 0; i < 10; i++) b.Circle(2.5f + i * 3.8f, g - 5.2f, 1.2f, AdobeWall);
                Banner(b, m, 6, g - 22);
                break;
            default:
                Minaret(b, m, 3, g, 30);
                Minaret(b, m, 34, g, 30);
                DomeHall(b, m, 20, g - 1, 20, 12);
                AdobeHouse(b, m, 7, g + 1, 8, 9);
                AdobeHouse(b, m, 25, g + 1, 8, 10);
                b.Rect(0, g + 1, 40, 2, new Color(170, 130, 86));
                break;
        }
    }

    // ------------------------------------------------------------------
    // Stilt: thatched houses raised on poles over water or wetland
    // ------------------------------------------------------------------

    private static readonly Color Thatch = new Color(210, 178, 98);
    private static readonly Color ThatchShade = new Color(168, 136, 70);
    private static readonly Color Bamboo = new Color(190, 156, 94);
    private static readonly Color Pole = new Color(96, 68, 42);

    private static void StiltHouse(Canvas b, Canvas m, float x, float g, float w, float h, float lift = 5f)
    {
        float floor = g - lift;
        for (float px = x + 0.6f; px < x + w; px += Math.Max(2.5f, (w - 1.2f) / 3f)) b.Rect(px, floor, 0.9f, lift + 1.5f, Pole);
        b.Rect(x - 0.6f, floor - 1.1f, w + 1.2f, 1.3f, Pole);
        b.Rect(x, floor - h, w, h - 1f, Bamboo);
        b.Rect(x + w * 0.62f, floor - h, w * 0.38f, h - 1f, ThatchShade);
        b.Rect(x + w * 0.38f, floor - h * 0.75f, w * 0.22f, h * 0.6f, AdobeDark);
        float roofH = w * 0.62f;
        var roof = new[] { V(x - 2.6f, floor - h + 1.2f), V(x + w / 2f, floor - h - roofH), V(x + w + 2.6f, floor - h + 1.2f) };
        b.Poly(Thatch, roof);
        m.Poly(new Color(255, 255, 255, 110), roof);
        b.Poly(ThatchShade, V(x + w / 2f, floor - h - roofH), V(x + w + 2.6f, floor - h + 1.2f), V(x + w / 2f, floor - h + 1.2f));
        for (float k = 0.35f; k < 1f; k += 0.3f)
            b.Line(V(x - 2.6f + (w / 2f + 2.6f) * k, floor - h + 1.2f - (roofH + 1.2f) * k), V(x - 2.6f + (w / 2f + 2.6f) * k + 1.8f, floor - h + 1.2f - (roofH + 1.2f) * k), 0.5f, ThatchShade);
    }

    private static void Longhouse(Canvas b, Canvas m, float x, float g, float w, float h)
    {
        float floor = g - 6;
        for (float px = x + 0.5f; px < x + w; px += 3.2f) b.Rect(px, floor, 1f, 7.5f, Pole);
        b.Rect(x - 0.5f, floor - 1.2f, w + 1f, 1.4f, Pole);
        b.Rect(x, floor - h, w, h - 1, Bamboo);
        b.Rect(x + w * 0.66f, floor - h, w * 0.34f, h - 1, ThatchShade);
        for (float wx = x + 2; wx < x + w - 2; wx += 4) b.Rect(wx, floor - h * 0.7f, 1.6f, 2f, AdobeDark);
        // Saddle roof with swept-up horns
        float top = floor - h;
        var roof = new[] { V(x - 3.5f, top - 8f), V(x + w * 0.28f, top - 3f), V(x + w * 0.72f, top - 3f), V(x + w + 3.5f, top - 8f),
            V(x + w + 1.2f, top + 1f), V(x - 1.2f, top + 1f) };
        TintedRoof(b, m, Thatch, ThatchShade, roof);
        b.Line(V(x - 3.5f, top - 8f), V(x - 1.2f, top + 1f), 0.6f, ThatchShade);
        b.Line(V(x + w + 3.5f, top - 8f), V(x + w + 1.2f, top + 1f), 0.6f, ThatchShade);
    }

    private static void Water(Canvas b, float x0, float x1, float g)
    {
        b.Rect(x0, g - 1.5f, x1 - x0, 4.5f, new Color(64, 128, 168));
        for (float x = x0 + 1; x < x1 - 2; x += 5) b.Rect(x, g - 0.2f, 2.4f, 0.6f, new Color(170, 215, 235));
    }

    private static void Stilt(Canvas b, Canvas m, int type, float g)
    {
        switch (type)
        {
            case 0:
                Water(b, 2, 38, g);
                StiltHouse(b, m, 5, g, 11, 7);
                StiltHouse(b, m, 23, g + 1, 11, 6, 4);
                break;
            case 1:
                Water(b, 1, 39, g);
                StiltHouse(b, m, 2, g + 1, 9, 6, 4);
                Longhouse(b, m, 15, g - 1, 11, 7);
                StiltHouse(b, m, 30, g + 1, 8, 6, 4);
                break;
            case 2:
                Water(b, 0, 40, g);
                StiltHouse(b, m, 1, g - 1, 8, 6, 6);
                StiltHouse(b, m, 31, g - 1, 8, 6, 6);
                Longhouse(b, m, 12, g - 2, 16, 9);
                b.Rect(9, g - 7, 22, 1.2f, Pole); // boardwalk
                Banner(b, m, 34, 5);
                break;
            default:
                Water(b, 0, 40, g + 1);
                Longhouse(b, m, 3, g - 4, 13, 8);
                Longhouse(b, m, 23, g - 4, 13, 9);
                StiltHouse(b, m, 14, g + 1, 11, 7, 4);
                b.Rect(0, g - 5, 40, 1.2f, Pole);
                Banner(b, m, 19, 1);
                break;
        }
    }

    // ------------------------------------------------------------------
    // Pagoda: white plaster, red posts, curved tiled roofs with upturned eaves
    // ------------------------------------------------------------------

    private static readonly Color Plaster = new Color(232, 226, 212);
    private static readonly Color PlasterShade = new Color(196, 188, 172);
    private static readonly Color Lacquer = new Color(176, 50, 40);
    private static readonly Color GreenTile = new Color(60, 88, 88);
    private static readonly Color GreenTileShade = new Color(40, 62, 64);

    private static void CurvedRoof(Canvas b, Canvas m, float x, float top, float w, float roofH, float flare = 2.6f)
    {
        var roof = new[] { V(x - flare, top - 1.8f), V(x - flare * 0.4f, top + 0.5f), V(x + w + flare * 0.4f, top + 0.5f), V(x + w + flare, top - 1.8f),
            V(x + w - w * 0.18f, top - roofH), V(x + w * 0.18f, top - roofH) };
        TintedRoof(b, m, GreenTile, GreenTileShade, roof);
        b.Rect(x + w * 0.18f, top - roofH - 0.6f, w * 0.64f, 0.9f, GreenTileShade);
    }

    private static void PagodaHouse(Canvas b, Canvas m, float x, float g, float w, float h)
    {
        b.Rect(x, g - h, w, h, Plaster);
        b.Rect(x + w * 0.64f, g - h, w * 0.36f, h, PlasterShade);
        b.Rect(x, g - h, 1f, h, Lacquer);
        b.Rect(x + w - 1f, g - h, 1f, h, Lacquer);
        b.Rect(x + w * 0.38f, g - h * 0.62f, w * 0.24f, h * 0.62f, Lacquer);
        CurvedRoof(b, m, x, g - h, w, w * 0.42f);
    }

    private static void PagodaTower(Canvas b, Canvas m, float cx, float g, int tiers, float baseW, float tierH)
    {
        float y = g;
        for (int i = 0; i < tiers; i++)
        {
            float w = baseW * (1f - i * 0.13f);
            float x = cx - w / 2;
            b.Rect(x, y - tierH, w, tierH, i == 0 ? Plaster : Lacquer);
            b.Rect(x + w * 0.6f, y - tierH, w * 0.4f, tierH, i == 0 ? PlasterShade : new Color(130, 36, 30));
            if (i == 0) b.Rect(cx - 1.3f, y - tierH * 0.7f, 2.6f, tierH * 0.7f, Lacquer);
            CurvedRoof(b, m, x, y - tierH, w, 2.2f, 2.4f);
            y -= tierH + 2.2f;
        }
        b.Rect(cx - 0.45f, y - 5f, 0.9f, 5.5f, new Color(210, 170, 70));
        for (int k = 0; k < 3; k++) b.Circle(cx, y - 1 - k * 1.5f, 0.9f, new Color(220, 180, 80));
    }

    private static void Pagoda(Canvas b, Canvas m, int type, float g)
    {
        switch (type)
        {
            case 0:
                Ground(b, 3, 37, g, new Color(120, 140, 80));
                PagodaHouse(b, m, 5, g, 12, 8);
                PagodaHouse(b, m, 23, g + 1, 11, 7);
                break;
            case 1:
                Ground(b, 2, 38, g, new Color(120, 140, 80));
                PagodaHouse(b, m, 2, g + 1, 10, 7);
                PagodaTower(b, m, 20, g, 3, 10, 5);
                PagodaHouse(b, m, 28, g + 1, 10, 8);
                break;
            case 2:
                PagodaHouse(b, m, 1, g - 3, 9, 7);
                PagodaHouse(b, m, 30, g - 3, 9, 7);
                PagodaTower(b, m, 20, g - 2, 4, 12, 4.8f);
                // Gate wall with a tiled coping
                b.Rect(1, g - 5, 38, 6, Plaster);
                b.Rect(1, g - 2, 38, 3, PlasterShade);
                b.Rect(0.5f, g - 6.2f, 39, 1.4f, GreenTile);
                b.Rect(17.5f, g - 4, 5, 5, Lacquer);
                Banner(b, m, 34, 6);
                break;
            default:
                PagodaTower(b, m, 20, g - 1, 5, 12, 4.4f);
                PagodaHouse(b, m, 0.5f, g - 1, 10, 10);
                PagodaHouse(b, m, 29.5f, g - 1, 10, 10);
                b.Rect(0, g + 1, 40, 2, new Color(120, 110, 100));
                // Paper lanterns
                b.Circle(12, g - 12, 1.2f, new Color(255, 120, 70));
                b.Circle(28, g - 12, 1.2f, new Color(255, 120, 70));
                break;
        }
    }

    // ------------------------------------------------------------------
    // Terraced: houses stacked on a terraced hillside, step temple on top
    // ------------------------------------------------------------------

    private static readonly Color TerraceGreen = new Color(118, 146, 74);
    private static readonly Color TerraceDark = new Color(84, 110, 54);
    private static readonly Color HillStone = new Color(196, 184, 160);
    private static readonly Color HillStoneShade = new Color(160, 146, 122);

    private static void Hill(Canvas b, float cx, float g, float halfW, float height)
    {
        b.Poly(TerraceGreen, V(cx - halfW, g + 1.5f), V(cx - halfW * 0.25f, g - height), V(cx + halfW * 0.25f, g - height), V(cx + halfW, g + 1.5f));
        // Terrace walls: horizontal steps
        for (float y = g - 3; y > g - height + 1; y -= 3.2f)
        {
            float t = (g + 1.5f - y) / (height + 1.5f);
            float hw = halfW - (halfW * 0.75f) * t;
            b.Rect(cx - hw + 0.4f, y, hw * 2 - 0.8f, 0.8f, TerraceDark);
        }
    }

    private static void FlatHouse(Canvas b, Canvas m, float x, float g, float w, float h)
    {
        b.Rect(x, g - h, w, h, HillStone);
        b.Rect(x + w * 0.62f, g - h, w * 0.38f, h, HillStoneShade);
        b.Rect(x - 0.3f, g - h - 1f, w + 0.6f, 1.3f, new Color(150, 100, 70));
        m.Rect(x - 0.3f, g - h - 1f, w + 0.6f, 1.3f, TrimMask);
        b.Rect(x + w * 0.3f, g - h * 0.65f, w * 0.25f, h * 0.35f, AdobeDark);
    }

    private static void StepTemple(Canvas b, Canvas m, float cx, float g, float scale)
    {
        float y = g;
        for (int i = 0; i < 3; i++)
        {
            float w = (12 - i * 3.2f) * scale, h = 3.4f * scale;
            b.Rect(cx - w / 2, y - h, w, h, HillStone);
            b.Rect(cx + w * 0.15f, y - h, w * 0.35f, h, HillStoneShade);
            y -= h;
        }
        b.Rect(cx - 0.8f * scale, g - 10 * scale, 1.6f * scale, 10 * scale, HillStoneShade); // stairway
        float sw = 4.2f * scale;
        b.Rect(cx - sw / 2, y - 3 * scale, sw, 3 * scale, HillStone);
        TintedRoof(b, m, Tile, TileShade, new[] { V(cx - sw / 2 - 1, y - 3 * scale), V(cx, y - 6.5f * scale), V(cx + sw / 2 + 1, y - 3 * scale) });
    }

    private static void Terraced(Canvas b, Canvas m, int type, float g)
    {
        switch (type)
        {
            case 0:
                Hill(b, 20, g, 18, 14);
                FlatHouse(b, m, 8, g - 1, 8, 6);
                FlatHouse(b, m, 20, g - 8, 7, 6);
                break;
            case 1:
                Hill(b, 20, g, 19, 20);
                FlatHouse(b, m, 4, g, 8, 6);
                FlatHouse(b, m, 26, g - 3, 8, 6);
                FlatHouse(b, m, 12, g - 9, 7, 5);
                StepTemple(b, m, 22, g - 13, 0.75f);
                break;
            case 2:
                Hill(b, 20, g, 20, 24);
                FlatHouse(b, m, 2, g, 8, 6);
                FlatHouse(b, m, 30, g, 8, 6);
                FlatHouse(b, m, 9, g - 7, 7, 6);
                FlatHouse(b, m, 25, g - 7, 7, 6);
                StepTemple(b, m, 20, g - 14, 1f);
                Banner(b, m, 34, 8);
                break;
            default:
                Hill(b, 20, g, 20, 27);
                FlatHouse(b, m, 0.5f, g + 1, 9, 8);
                FlatHouse(b, m, 30.5f, g + 1, 9, 8);
                FlatHouse(b, m, 10, g - 5, 8, 7);
                FlatHouse(b, m, 23, g - 5, 8, 7);
                FlatHouse(b, m, 6, g - 12, 6, 5);
                FlatHouse(b, m, 28, g - 12, 6, 5);
                StepTemple(b, m, 20, g - 16, 1.15f);
                break;
        }
    }

    // ------------------------------------------------------------------
    // Modern: concrete and glass
    // ------------------------------------------------------------------

    private static readonly Color Concrete = new Color(206, 206, 210);
    private static readonly Color ConcreteShade = new Color(160, 162, 170);

    private static void Tower(Canvas b, Canvas m, float x, float g, float w, float h, bool glass = true)
    {
        float top = g - h;
        b.Rect(x, top, w, h, glass ? Glass : Concrete);
        b.Rect(x + w * 0.6f, top, w * 0.4f, h, glass ? GlassDark : ConcreteShade);
        for (float wy = top + 3; wy < g - 2; wy += 3)
            for (float wx = x + 1.2f; wx < x + w - 1.5f; wx += 2.2f)
                b.Rect(wx, wy, 1.1f, 1.3f, glass ? new Color(255, 236, 150, 220) : new Color(110, 140, 170));
        b.Rect(x, top, w, 2.2f, new Color(120, 120, 120));
        m.Rect(x, top, w, 2.2f, MaskLight);
    }

    private static void ModernHouse(Canvas b, Canvas m, float x, float g, float w, float h)
    {
        b.Rect(x, g - h, w, h, Concrete);
        b.Rect(x + w * 0.62f, g - h, w * 0.38f, h, ConcreteShade);
        b.Rect(x + w * 0.12f, g - h * 0.7f, w * 0.42f, h * 0.38f, Glass);
        b.Rect(x - 0.6f, g - h - 1.4f, w + 1.2f, 1.6f, new Color(120, 120, 120));
        m.Rect(x - 0.6f, g - h - 1.4f, w + 1.2f, 1.6f, MaskLight);
    }

    private static void Tree(Canvas b, float x, float g, float r)
    {
        b.Rect(x - 0.4f, g - r, 0.8f, r, new Color(100, 76, 50));
        b.Circle(x, g - r * 1.3f, r * 0.8f, new Color(80, 150, 70));
    }

    private static void Modern(Canvas b, Canvas m, int type, float g)
    {
        switch (type)
        {
            case 0:
                b.Rect(2, g, 36, 2, new Color(90, 90, 96));
                ModernHouse(b, m, 4, g, 12, 7);
                ModernHouse(b, m, 22, g, 12, 8);
                Tree(b, 19, g, 3.5f);
                break;
            case 1:
                b.Rect(1, g, 38, 2, new Color(90, 90, 96));
                ModernHouse(b, m, 2, g, 10, 8);
                Tower(b, m, 14, g, 10, 20, false);
                ModernHouse(b, m, 27, g, 11, 9);
                break;
            case 2:
                b.Rect(0, g, 40, 2, new Color(90, 90, 96));
                Tower(b, m, 3, g, 8, 16, false);
                Tower(b, m, 12, g, 9, 28);
                Tower(b, m, 22, g, 8, 22);
                Tower(b, m, 31, g, 7, 13, false);
                break;
            default:
                float[] xs = { 2, 9, 15, 23, 30 };
                float[] ws = { 7, 6, 8, 7, 8 };
                float[] hs = { 14, 24, 32, 20, 12 };
                for (int i = 0; i < xs.Length; i++) Tower(b, m, xs[i], g, ws[i], hs[i]);
                b.Rect(18.6f, g - 38, 0.9f, 6, new Color(80, 80, 90));
                b.Circle(19, g - 38, 1f, new Color(255, 80, 80));
                b.Rect(0, g, 40, 2, new Color(90, 90, 96));
                break;
        }
    }

    // ------------------------------------------------------------------
    // Futuristic: white domes, glass spires, maglev rings, cyan lights
    // ------------------------------------------------------------------

    private static readonly Color Ceramic = new Color(234, 240, 246);
    private static readonly Color CeramicShade = new Color(188, 200, 214);
    private static readonly Color Cyan = new Color(90, 240, 255);

    private static void FutureDome(Canvas b, Canvas m, float cx, float g, float r)
    {
        b.Circle(cx, g, r, Ceramic);
        b.Poly(CeramicShade, V(cx, g - r), V(cx + r, g), V(cx, g));
        b.Circle(cx - r * 0.25f, g - r * 0.45f, r * 0.4f, new Color(150, 214, 240));
        b.Rect(cx - r, g - r * 0.25f, r * 2, r * 0.18f + 0.6f, new Color(128, 128, 128));
        m.Rect(cx - r, g - r * 0.25f, r * 2, r * 0.18f + 0.6f, MaskLight);
        b.ErasePoly(V(cx - r - 1, g + 0.01f), V(cx + r + 1, g + 0.01f), V(cx + r + 1, g + r + 1), V(cx - r - 1, g + r + 1));
    }

    private static void Spire(Canvas b, Canvas m, float cx, float g, float h, float w)
    {
        b.Poly(Ceramic, V(cx - w / 2, g), V(cx - w * 0.2f, g - h), V(cx + w * 0.2f, g - h), V(cx + w / 2, g));
        b.Poly(CeramicShade, V(cx, g), V(cx, g - h), V(cx + w * 0.2f, g - h), V(cx + w / 2, g));
        for (float y = g - 4; y > g - h + 2; y -= 4) b.Rect(cx - w * 0.25f, y, w * 0.5f, 0.7f, Cyan);
        b.Rect(cx - 0.35f, g - h - 4, 0.7f, 4, CeramicShade);
        b.Circle(cx, g - h - 4, 1f, Cyan);
    }

    private static void Futuristic(Canvas b, Canvas m, int type, float g)
    {
        switch (type)
        {
            case 0:
                b.Rect(2, g, 36, 1.6f, CeramicShade);
                FutureDome(b, m, 12, g, 7);
                FutureDome(b, m, 28, g, 6);
                break;
            case 1:
                b.Rect(1, g, 38, 1.6f, CeramicShade);
                FutureDome(b, m, 8, g, 6.5f);
                Spire(b, m, 20, g, 24, 6);
                FutureDome(b, m, 31, g, 7);
                break;
            case 2:
                b.Rect(0, g, 40, 1.6f, CeramicShade);
                FutureDome(b, m, 20, g, 11);
                Spire(b, m, 7, g, 26, 5);
                Spire(b, m, 33, g, 22, 5);
                // Maglev ring
                b.Rect(3, g - 15, 34, 1.4f, new Color(128, 128, 128));
                m.Rect(3, g - 15, 34, 1.4f, MaskLight);
                break;
            default:
                // Arcology: a terraced glass pyramid between spires
                b.Poly(Ceramic, V(6, g), V(20, g - 30), V(34, g));
                b.Poly(CeramicShade, V(20, g), V(20, g - 30), V(34, g));
                for (float y = g - 4; y > g - 28; y -= 4)
                {
                    float hw = (g - y) / 30f * 14f;
                    b.Rect(20 - (14 - hw) + 0.5f, y, (14 - hw) * 2 - 1f, 1.1f, new Color(120, 200, 235));
                }
                b.Poly(new Color(128, 128, 128), V(17, g - 25), V(20, g - 30), V(23, g - 25));
                m.Poly(MaskLight, V(17, g - 25), V(20, g - 30), V(23, g - 25));
                Spire(b, m, 3.5f, g, 30, 4.5f);
                Spire(b, m, 36.5f, g, 26, 4.5f);
                b.Circle(20, g - 31.5f, 1.3f, Cyan);
                b.Rect(0, g, 40, 1.6f, CeramicShade);
                break;
        }
    }

    // ------------------------------------------------------------------
    // Specialization badges (24 px, drawn on a small disc next to the settlement)
    // ------------------------------------------------------------------

    private static Texture2D BuildBadge(GraphicsDevice device, CitySpecialization spec)
    {
        var c = new Canvas(SmallIconSize);
        switch (spec)
        {
            case CitySpecialization.Farming:
            {
                // Ploughed field in perspective with a red barn
                var soil = new Color(150, 104, 60);
                var crop = new Color(236, 200, 80);
                c.Poly(soil, V(1, 22), V(23, 22), V(19, 12), V(5, 12));
                for (int i = 0; i < 4; i++)
                {
                    float t0 = i / 4f + 0.04f, t1 = (i + 0.55f) / 4f;
                    c.Poly(i % 2 == 0 ? crop : new Color(120, 170, 70),
                        V(1 + 4 * t0, 22 - 10 * t0), V(23 - 4 * t0, 22 - 10 * t0), V(23 - 4 * t1, 22 - 10 * t1), V(1 + 4 * t1, 22 - 10 * t1));
                }
                c.Rect(13, 6, 8, 6, new Color(190, 50, 40));
                c.Poly(new Color(120, 36, 30), V(12, 6.5f), V(17, 2), V(22, 6.5f));
                c.Rect(15.8f, 8.5f, 2.4f, 3.5f, new Color(240, 230, 210));
                break;
            }
            case CitySpecialization.Fishing:
            {
                c.Rect(2, 17, 20, 3, new Color(70, 140, 190));
                c.Poly(new Color(150, 100, 60), V(4, 13), V(20, 13), V(17, 18), V(7, 18));
                c.Rect(11.4f, 3, 1.1f, 10, new Color(90, 64, 40));
                c.Poly(new Color(240, 236, 220), V(12.5f, 4), V(19, 11.5f), V(12.5f, 11.5f));
                // Fish
                c.Poly(new Color(200, 220, 235), V(3, 8), V(7, 6), V(9, 8), V(7, 10));
                c.Poly(new Color(200, 220, 235), V(2, 6.5f), V(3.4f, 8), V(2, 9.5f));
                break;
            }
            case CitySpecialization.Port:
            {
                var steel = new Color(240, 170, 60);
                c.Rect(2, 18, 20, 3.5f, new Color(70, 140, 190));
                c.Rect(4, 5, 1.8f, 14, steel);
                c.Line(V(4.9f, 5.5f), V(18, 5.5f), 1.6f, steel);
                c.Line(V(4.9f, 10f), V(10, 5.5f), 1.0f, steel);
                c.Rect(16.4f, 5.5f, 0.7f, 5, new Color(60, 60, 60));
                c.Rect(14.5f, 10.5f, 4.5f, 3, new Color(200, 60, 50));
                c.Poly(new Color(80, 90, 110), V(9, 15), V(22, 15), V(20, 19), V(10, 19));
                c.Rect(12, 13, 3, 2, new Color(60, 150, 90));
                c.Rect(15.5f, 13, 3, 2, new Color(70, 110, 200));
                break;
            }
            case CitySpecialization.Mining:
            {
                var wood = new Color(120, 88, 56);
                c.Line(V(5, 21), V(12, 4), 1.6f, wood);
                c.Line(V(19, 21), V(12, 4), 1.6f, wood);
                c.Line(V(7.5f, 15), V(16.5f, 15), 1.3f, wood);
                c.Line(V(9.5f, 10), V(14.5f, 10), 1.2f, wood);
                c.Ring(12, 5, 2.8f, 1.6f, new Color(200, 200, 210));
                c.Poly(new Color(90, 80, 76), V(2, 22), V(7, 17), V(10, 22));
                // Pickaxe
                c.Line(V(14, 22), V(21, 15), 1.2f, wood);
                c.Poly(new Color(210, 214, 224), V(17, 13.5f), V(22.5f, 15), V(23, 19), V(21.5f, 16.5f));
                break;
            }
            case CitySpecialization.Timber:
            {
                var bark = new Color(132, 88, 50);
                var cut = new Color(226, 186, 128);
                // Stacked logs with their cut ends showing
                foreach (var (lx, ly) in new (float, float)[] { (3, 18), (3, 12.4f) })
                {
                    c.Rect(lx, ly - 2.7f, 15, 5.4f, bark);
                    c.Circle(lx + 15, ly, 2.8f, cut);
                    c.Ring(lx + 15, ly, 1.6f, 1.1f, bark);
                }
                // Tree
                c.Poly(new Color(60, 130, 70), V(8, 2), V(13, 9), V(3, 9));
                break;
            }
            case CitySpecialization.Trade:
            {
                c.Rect(4, 10, 1.4f, 11, new Color(110, 80, 50));
                c.Rect(18.6f, 10, 1.4f, 11, new Color(110, 80, 50));
                var awning = new[] { V(2, 11), V(5, 4), V(19, 4), V(22, 11) };
                c.Poly(new Color(230, 230, 220), awning);
                for (int i = 0; i < 4; i++)
                {
                    float x0 = 2 + i * 5f;
                    c.Poly(new Color(210, 60, 50), V(x0 + (i == 0 ? 0.8f : 0), 11), V(x0 + 3 - i * 0.2f + 0.6f, 4), V(x0 + 5.5f - i * 0.2f, 4), V(x0 + 2.5f, 11));
                }
                c.Rect(4, 15, 16, 2.4f, new Color(150, 110, 70));
                c.Circle(8, 14, 1.6f, new Color(240, 180, 60));
                c.Circle(12, 14, 1.6f, new Color(120, 190, 80));
                c.Circle(16, 14, 1.6f, new Color(230, 90, 60));
                break;
            }
            case CitySpecialization.Industrial:
            {
                var brick = new Color(150, 96, 72);
                var dark = new Color(110, 70, 56);
                c.Rect(15, 3, 3.4f, 12, dark);
                c.Rect(15, 5, 3.4f, 1, new Color(230, 230, 230));
                c.Poly(brick, V(2, 22), V(2, 13), V(6, 10), V(6, 13), V(10, 10), V(10, 13), V(14, 10), V(14, 13), V(21, 13), V(21, 22));
                c.Rect(2, 19, 19, 3, dark);
                c.Rect(4, 15, 2.4f, 2.4f, Window);
                c.Rect(9, 15, 2.4f, 2.4f, Window);
                c.Rect(15, 15, 2.4f, 2.4f, Window);
                break;
            }
            case CitySpecialization.Academic:
            {
                var marble = new Color(236, 232, 222);
                c.Circle(12, 10, 5.5f, new Color(90, 150, 200));
                c.Rect(11.6f, 2, 0.8f, 3, marble);
                c.Rect(4, 10, 16, 2, marble);
                for (int i = 0; i < 4; i++) c.Rect(5 + i * 4.2f, 12, 1.6f, 7, marble);
                c.Rect(3, 19, 18, 2.4f, marble);
                // Mortarboard
                c.Poly(new Color(40, 40, 50), V(14, 3.5f), V(19, 1.5f), V(24, 3.5f), V(19, 5.5f));
                c.Rect(23, 3.5f, 0.6f, 3, new Color(240, 200, 60));
                break;
            }
            case CitySpecialization.Holy:
            {
                var gold = new Color(250, 206, 80);
                c.Poly(new Color(230, 224, 208), V(5, 21), V(5, 11), V(12, 6), V(19, 11), V(19, 21));
                c.Poly(gold, V(4, 11.5f), V(12, 5), V(20, 11.5f), V(18.5f, 12), V(12, 7.2f), V(5.5f, 12));
                c.Rect(10, 14, 4, 7, new Color(110, 70, 40));
                c.Circle(12, 14, 2, new Color(110, 70, 40));
                c.Rect(11.3f, 0.8f, 1.4f, 4.5f, gold);
                c.Rect(9.8f, 2, 4.4f, 1.3f, gold);
                break;
            }
            case CitySpecialization.Fortress:
            {
                c.Rect(3, 10, 18, 11, Stone);
                c.Rect(14, 10, 7, 11, StoneDark);
                for (int i = 0; i < 5; i++) c.Rect(3 + i * 3.8f, 7.5f, 2.2f, 3, Stone);
                c.Rect(8.5f, 3, 7, 8, Stone);
                for (int i = 0; i < 3; i++) c.Rect(8.5f + i * 2.6f, 1, 1.8f, 2.4f, Stone);
                c.Rect(10.5f, 15, 3, 6, Door);
                c.Circle(12, 15, 1.5f, Door);
                c.Rect(11.3f, 5, 1.4f, 2.4f, new Color(40, 40, 48));
                break;
            }
            default: // Capital
            {
                var gold = new Color(255, 205, 60);
                c.Poly(gold, V(3, 18), V(3, 8), V(8, 13), V(12, 5), V(16, 13), V(21, 8), V(21, 18));
                c.Rect(3, 17, 18, 3, new Color(220, 160, 40));
                c.Circle(12, 15, 1.6f, new Color(220, 40, 60));
                break;
            }
        }
        c.Outline(1.0f, Outline);
        return c.ToTexture(device);
    }
}

namespace Idefix.Core.Mascot;

public static class PixelTerrier
{
    private const int Top = 1;
    private const int Bottom = 4;
    private const int HeadRows = 17;
    private const int HeadColumns = 21;
    private const int FrontSplit = 13;
    private const double Beat = 0.18;
    private const int PageColumn = 10;

    private static readonly string[] TailUp =
    [
        "...K................K.....",
        "..KWK..............KWK....",
        "..KWSK............KSWK....",
        ".KWSSWK.KK....KK.KWSSWK...",
        ".KWSSWWKWWKKKKWWKWWSSWK...",
        "KWWWWWWWWWWWWWWWWWWWWWWK..",
        "KWWWWWWWWWWWWWWWWWWWWWWK..",
        "KWWWGGGWWWWWWWWWWGGGWWWK..",
        "KWWWWEOWWWWWWWWWWOEWWWWK..",
        "KWWWWEEWWWWWWWWWWEEWWWWK..",
        "KWWWWWWWWWSSSSWWWWWWWWWK..",
        "KWWWWWWWWSSNNSSWWWWWWWWK..",
        "WKWWWWWWWWSNNSWWWWWWWWKW..",
        "KWWWWWWWWWWKKWWWWWWWWWWK..",
        ".KWWWWWWWWWPPWWWWWWWWWK...",
        "..WKWWWWWWWWWWWWWWWWKWK...",
        "...KWKWWWWWWWWWWWWKWKKWK..",
        ".....KRRRRRYYRRRRRK..KWK..",
        "....KWWWWWWWWWWWWWWK.WK...",
        "...KWWWWWWWWWWWWWWWWKWK...",
        "...KSWWWWWWWWWWWWWWSKK....",
        "...KSWWWKWWWWWWKWWWSK.....",
        "...KSWWWKWWWWWWKWWWSK.....",
        "..KWWWWWKWWWWWWKWWWWWK....",
        "..KKKKKKK.KKKK.KKKKKKK....",
    ];

    private static readonly string[] TailSide =
    [
        "...K................K.....",
        "..KWK..............KWK....",
        "..KWSK............KSWK....",
        ".KWSSWK.KK....KK.KWSSWK...",
        ".KWSSWWKWWKKKKWWKWWSSWK...",
        "KWWWWWWWWWWWWWWWWWWWWWWK..",
        "KWWWWWWWWWWWWWWWWWWWWWWK..",
        "KWWWGGGWWWWWWWWWWGGGWWWK..",
        "KWWWWEOWWWWWWWWWWOEWWWWK..",
        "KWWWWEEWWWWWWWWWWEEWWWWK..",
        "KWWWWWWWWWSSSSWWWWWWWWWK..",
        "KWWWWWWWWSSNNSSWWWWWWWWK..",
        "WKWWWWWWWWSNNSWWWWWWWWKW..",
        "KWWWWWWWWWWKKWWWWWWWWWWK..",
        ".KWWWWWWWWWPPWWWWWWWWWK...",
        "..WKWWWWWWWWWWWWWWWWKW....",
        "...KWKWWWWWWWWWWWWKWK.....",
        ".....KRRRRRYYRRRRRK.....K.",
        "....KWWWWWWWWWWWWWWK..KWK.",
        "...KWWWWWWWWWWWWWWWWKWWK..",
        "...KSWWWWWWWWWWWWWWSKKK...",
        "...KSWWWKWWWWWWKWWWSK.....",
        "...KSWWWKWWWWWWKWWWSK.....",
        "..KWWWWWKWWWWWWKWWWWWK....",
        "..KKKKKKK.KKKK.KKKKKKK....",
    ];

    private static readonly string[] Page =
    [
        "GGGGG..",
        "GQQQQGG",
        "GQLLLQG",
        "GQQQQQG",
        "GQLLLQG",
        "GQLLQQG",
        "GGGGGGG",
    ];

    private static readonly (int Row, int Column)[] Spray =
    [
        (0, 2), (-1, 0), (1, 4), (0, 23), (-1, 25), (1, 21), (2, 7), (2, 18),
    ];

    private enum Legs
    {
        Down,
        LeftUp,
        RightUp,
    }

    public static double Cycle => 7.0;

    public static int Width => TailUp[0].Length;

    public static int Height => TailUp.Length;

    public static int CanvasWidth => Width;

    public static int CanvasHeight => Height + Top + Bottom;

    public static char[][] Compose(double seconds)
    {
        var t = seconds % Cycle;
        var beat = (int)(t / Beat);
        var tail = beat % 2 == 0 ? TailUp : TailSide;
        var canvas = Enumerable.Range(0, CanvasHeight).Select(_ => Enumerable.Repeat('.', CanvasWidth).ToArray()).ToArray();

        if (t < 1.4)
        {
            Draw(canvas, (int)(t / 0.32) % 2 == 0 ? TailUp : TailSide, headDrop: 0, bob: 0, Legs.Down);
        }
        else if (t < 2.6)
        {
            Draw(canvas, tail, headDrop: 2, bob: 0, Legs.Down);
            if (beat % 2 == 0)
            {
                Stamp(canvas, ["A.....A"], 15, 9);
            }
        }
        else if (t < 4.0)
        {
            Draw(canvas, tail, headDrop: 1, bob: 0, beat % 2 == 0 ? Legs.LeftUp : Legs.RightUp);
            DrawDirt(canvas, beat);
        }
        else if (t < 4.6)
        {
            Draw(canvas, tail, headDrop: 2, bob: 0, Legs.Down);
            var rise = Math.Min(1, (t - 4.0) / 0.4);
            Stamp(canvas, Page, Height + 3 - (int)Math.Round(rise * 3), PageColumn);
        }
        else
        {
            var bob = beat % 2 == 0 ? -1 : 0;
            Draw(canvas, tail, headDrop: 0, bob, beat % 2 == 0 ? Legs.LeftUp : Legs.RightUp);
            Stamp(canvas, Page, 12 + bob, PageColumn);
        }

        return canvas;
    }

    private static void Draw(char[][] canvas, string[] frame, int headDrop, int bob, Legs legs)
    {
        for (var row = 0; row < Height; row++)
        {
            for (var column = 0; column < Width; column++)
            {
                if (row >= HeadRows || column >= HeadColumns)
                {
                    Put(canvas, row + bob, column, BodyPixel(frame, row, column, legs));
                }
            }
        }

        for (var row = 0; row < HeadRows; row++)
        {
            for (var column = 0; column < HeadColumns; column++)
            {
                Put(canvas, row + bob + headDrop, column, frame[row][column]);
            }
        }
    }

    private static char BodyPixel(string[] frame, int row, int column, Legs legs)
    {
        var lifted = column < HeadColumns && (legs == Legs.LeftUp ? column < FrontSplit : legs == Legs.RightUp && column >= FrontSplit);
        if (!lifted || row < 20)
        {
            return frame[row][column];
        }

        return row + 1 < Height ? frame[row + 1][column] : '.';
    }

    private static void DrawDirt(char[][] canvas, int beat)
    {
        for (var i = 0; i < Spray.Length; i++)
        {
            if ((i + beat) % 3 != 0)
            {
                Put(canvas, Height + Spray[i].Row - (beat % 2), Spray[i].Column, 'D');
            }
        }

        Stamp(canvas, ["..DDDDDDDDDDDDDDDDDDDD.."], Height, 1);
    }

    private static void Stamp(char[][] canvas, string[] rows, int row, int column)
    {
        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                Put(canvas, row + r, column + c, rows[r][c]);
            }
        }
    }

    private static void Put(char[][] canvas, int row, int column, char pixel)
    {
        var y = row + Top;
        if (pixel != '.' && y >= 0 && y < CanvasHeight && column >= 0 && column < CanvasWidth)
        {
            canvas[y][column] = pixel;
        }
    }
}

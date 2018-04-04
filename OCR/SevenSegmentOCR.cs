using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml.Media.Imaging;

namespace SupernovaIC.OCR
{
    public class SevenSegmentOCR
    {
        const int BLACK = 0;
        const int WHITE = 255;
        const int DEFAULT = 140;
        const int GREEN_SCREEN = 50;
        const int LIGHT_GREEN = 115;
        const int BLUE = 70;
        private class Limits
        {
            public int x1 { get; set; }
            public int x2 { get; set; }
            public int y1 { get; set; }
            public int y2 { get; set; }
        }

        private class Pair
        {
            public int v1 { get; set; }
            public int v2 { get; set; }

            public Pair()
            {

            }

            public Pair(int V1, int V2)
            {
                v1 = V1;
                v2 = V2;
            }
        }

        WriteableBitmap Bmp;

        public SevenSegmentOCR(WriteableBitmap bmp)
        {
            Bmp = bmp;
        }

        public WriteableBitmap BlackWhite(int type = 0, bool faded = false)
        {
            byte limit = 0;
            switch (type)
            {
                case 0:
                    limit = DEFAULT;
                    break;
                case 1:
                    limit = GREEN_SCREEN;
                    break;
                case 2:
                    limit = BLUE;
                    break;
            }

            if (faded)
                limit += 75;

            if (limit > 255)
                limit = 255;

            var img1 = Bmp;
            int width1 = img1.PixelWidth;
            int height1 = img1.PixelHeight;

            Parallel.For(0, width1, i =>
            {
                Parallel.For(0, height1, j =>
                {
                    Color color1 = Bmp.GetPixel(i, j);
                    int r1 = (int)color1.R;
                    int g1 = (int)color1.G;
                    int b1 = (int)color1.B;
                    byte a, r, g, b;
                    int ans = (r1 + g1 + b1) / 3;
                    if (ans < limit)
                    {
                        a = BLACK;
                        r = BLACK;
                        g = BLACK;
                        b = BLACK;
                    }
                    else
                    {
                        a = WHITE;
                        r = WHITE;
                        g = WHITE;
                        b = WHITE;
                    }
                    color1 = Color.FromArgb(a, r, g, b);
                    img1.SetPixel(i, j, color1);
                });
            });

            return Bmp;
        }

        public string recognizeNumber()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            try
            {
                string number = String.Empty;

                List<Limits> numbers = new List<Limits>();

                Pair boundariesX = new Pair(0, 0);
                Pair boundariesY = new Pair(0, 0);

                int i = 0;
                while (true)
                {
                    if (sw.ElapsedMilliseconds > 1000 * 60 * 10)
                        goto Exit;
                    try
                    {
                        if (numbers.Count > 0)
                            boundariesX = getBoundariesX(numbers[i - 1].x2);
                        else
                            boundariesX = getBoundariesX();

                        boundariesY = getBoundariesY(boundariesX);

                        numbers.Add(new Limits() { x1 = boundariesX.v1, x2 = boundariesX.v2, y1 = boundariesY.v1, y2 = boundariesY.v2 });

                        i++;
                        if (i > 6)
                            goto Exit;
                    }
                    //If there is no more data to look up, it breaks the while
                    catch
                    {
                        break;
                    }
                }

                #region Check Numbers
                sw = new Stopwatch();
                foreach (Limits val in numbers)
                {
                    if (sw.ElapsedMilliseconds > 1000 * 60 * 10)
                        goto Exit;

                    //A digit that has a width of less than one quarter of it's height is recognized as a one.
                    if ((val.x2 - val.x1) <= (int)(val.y2 - val.y1) / 4)
                    {
                        number += "1";
                    }
                    //To recognize a minus sign a method similar to recognizing the digit one is used. If a digit is less high than 1/3 of its width, it is considered a minus sign.
                    else if ((val.y2 - val.y1) <= (int)(val.x2 - val.x1) / 3)
                    {
                        number += "-";
                    }
                    //To recognize a decimal point, e.g. of a digital scale, the size of each digit (that was not recognized as a one already) is compared with the maximum digit width and height. If a digit is significantly smaller than that, it is assumed to be a decimal point. The decimal point or thousands separators count towards the number of digits to recognize.
                    else if ((val.y2 - val.y1) <= (val.x2 - val.x1))
                    {
                        number += ".";
                    }
                    /*
                    * Every digit found by the segmentation is classified as follows: A vertical scan is started in the center top pixel of the digit to find the three horizontal segments. Any foreground pixel in the upper third is counted as part of the top segment, those in the second third as part of the middle and those in the last third as part of the bottom segment.
                    * To examine the vertical segments two horizontal scanlines starting on the left margin of the digit are used. The first starts a quarter of the digit height from the top, the other from a quarter of the digit height from the bottom. Foreground pixels in the left resp. right half represent left resp. right segments.
                    * The recognized segments are then used to identify the displayed digit using a table lookup (implemented as a switch statement).
                    */
                    else
                    {
                        number += detectNumber(val);
                    }
                }
                #endregion

                sw.Stop();
                return number;
            }
            catch
            {
                sw.Stop();
                return "unknown";
            }
        Exit:
            sw.Stop();
            return "unknown";
        }

        private Pair getBoundariesX(int Start = BLACK)
        {
            int x1 = getNewLocationX(Start, 0);
            int x2 = 0;

            for (int x = x1; x < Bmp.PixelWidth; x++)
            {
                int avg = 0;

                x2 = x;
                for (int y = 0; y < Bmp.PixelHeight; y++)
                    avg += getColor(x, y);

                if (avg / Bmp.PixelHeight == 255)
                    break;
            }

            return new Pair(x1, x2--);
        }

        private int getNewLocationX(int Start = 0, byte color = BLACK)
        {
            for (int x = Start; x < Bmp.PixelWidth; x++)
            {
                for (int y = 0; y < Bmp.PixelHeight; y++)
                {
                    if (getColor(x, y) == color)
                        return x;
                }
            }
            return -1;
        }

        private Pair getBoundariesY(Pair xLimits)
        {
            int y1 = 0;
            int y2 = 0;

            bool found = false;
            for (int y = 0; y < Bmp.PixelHeight; y++)
            {
                for (int x = xLimits.v1; x < xLimits.v2; x++)
                {
                    if (getColor(x, y) == BLACK)
                    {
                        y1 = y;
                        found = true;
                        break;
                    }
                }

                if (found)
                    break;
            }

            found = false;
            for (int y = Bmp.PixelHeight - 1; y >= 0; y--)
            {
                for (int x = xLimits.v1; x < xLimits.v2; x++)
                {
                    if (getColor(x, y) == BLACK)
                    {
                        y2 = y;
                        found = true;
                        break;
                    }
                }

                if (found)
                    break;
            }

            return new Pair(y1, y2);
        }

        private string detectNumber(Limits number)
        {
            int[] YResults = new int[3];
            YResults = getYResults(number);

            int[,] XResultsUp = new int[1, 2];//[0,0]=left location, [0,1]=right location
            XResultsUp = getXResults(number, true);

            int[,] XResultsDown = new int[1, 2];//[0,0]=left location, [0,1]=right location
            XResultsDown = getXResults(number, false);

            if (YResults[0] == 1 && YResults[1] == 0 && YResults[2] == 1 && XResultsUp[0, 0] == 1 && XResultsUp[0, 1] == 1 && XResultsDown[0, 0] == 1 && XResultsDown[0, 1] == 1)
                return "0";
            else if (YResults[0] == 1 && YResults[1] == 1 && YResults[2] == 1 && XResultsUp[0, 0] == 0 && XResultsUp[0, 1] == 1 && XResultsDown[0, 0] == 1 && XResultsDown[0, 1] == 0)
                return "2";
            else if (YResults[0] == 1 && YResults[1] == 1 && YResults[2] == 1 && XResultsUp[0, 0] == 0 && XResultsUp[0, 1] == 1 && XResultsDown[0, 0] == 0 && XResultsDown[0, 1] == 1)
                return "3";
            else if (YResults[0] == 0 && YResults[1] == 1 && YResults[2] == 0 && XResultsUp[0, 0] == 1 && XResultsUp[0, 1] == 1 && XResultsDown[0, 0] == 0 && XResultsDown[0, 1] == 1)
                return "4";
            else if (YResults[0] == 1 && YResults[1] == 1 && YResults[2] == 1 && XResultsUp[0, 0] == 1 && XResultsUp[0, 1] == 0 && XResultsDown[0, 0] == 0 && XResultsDown[0, 1] == 1)
                return "5";
            else if (YResults[0] == 1 && YResults[1] == 1 && YResults[2] == 1 && XResultsUp[0, 0] == 1 && XResultsUp[0, 1] == 0 && XResultsDown[0, 0] == 1 && XResultsDown[0, 1] == 1)
                return "6";
            else if (YResults[0] == 1 && YResults[1] == 0 && YResults[2] == 0 && XResultsUp[0, 0] == 0 && XResultsUp[0, 1] == 1 && XResultsDown[0, 0] == 0 && XResultsDown[0, 1] == 1)
                return "7";
            else if (YResults[0] == 1 && YResults[1] == 1 && YResults[2] == 1 && XResultsUp[0, 0] == 1 && XResultsUp[0, 1] == 1 && XResultsDown[0, 0] == 1 && XResultsDown[0, 1] == 1)
                return "8";
            else if (YResults[0] == 1 && YResults[1] == 1 && YResults[2] == 1 && XResultsUp[0, 0] == 1 && XResultsUp[0, 1] == 1 && XResultsDown[0, 0] == 0 && XResultsDown[0, 1] == 1)
                return "9";
            else
                return "―";
        }

        private int[] getYResults(Limits number)
        {
            int center = (number.x1 + number.x2) / 2;

            int[] total = new int[3] { 0, 0, 0 };

            for (int y = 0; y < Bmp.PixelHeight / 4; y++)
            {

                if (getColor(center, y) == BLACK)
                {
                    total[0] = 1;
                    break;
                }
            }

            for (int y = Bmp.PixelHeight / 2; y < Bmp.PixelHeight - Bmp.PixelHeight / 4; y++)
            {

                if (getColor(center, y) == BLACK)
                {
                    total[1] = 1;
                    break;
                }
            }

            for (int y = Bmp.PixelHeight - 1; y >= Bmp.PixelHeight - Bmp.PixelHeight / 4; y--)
            {

                if (getColor(center, y) == BLACK)
                {
                    total[2] = 1;
                    break;
                }
            }

            return total;
        }

        private int getNewLocationY(int Center, int Start = 0, byte color = WHITE)
        {
            for (int y = Start; y < Bmp.PixelHeight; y++)
            {
                if (getColor(Center, y) == color)
                    return y;
            }
            return -1;
        }

        private int[,] getXResults(Limits number, bool Up)
        {
            int[,] XResults = new int[1, 2];
            int value = (number.y2 - number.y1) / 3;

            if (Up == false)
                value = number.y2 - value;
            else
                value += number.y1;

            for (int x = number.x1; x < number.x1 + (number.x2 - number.x1) / 2; x++)
            {
                if (getColor(x, value) == BLACK)
                {
                    XResults[0, 0] = 1;
                    break;
                }
            }

            for (int x = number.x2; x >= number.x2 - (number.x2 - number.x1) / 2; x--)
            {
                if (getColor(x, value) == BLACK)
                {
                    XResults[0, 1] = 1;
                    break;
                }
            }

            return XResults;
        }

        private int getColor(int x, int y)
        {
            return (int)(Bmp.GetPixel(x, y).R + Bmp.GetPixel(x, y).G + Bmp.GetPixel(x, y).B) / 3;
        }
    }
}
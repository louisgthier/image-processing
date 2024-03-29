﻿using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
namespace ImageProcessing
{
    /// <summary>
    /// A MyImage containing a QR Code
    /// </summary>
    public class QRCode : MyImage
    {
        /// <summary>
        /// Mask pattern formulas
        /// </summary>
        static Func<int, int, bool>[] masks = new Func<int, int, bool>[] {
                (i, j) => (i + j) % 2 == 0,
                (i, j) => (i) % 2 == 0,
                (i, j) => (j) % 3 == 0,
                (i, j) => (i + j) % 3 == 0,
                (i, j) => (i/2 + j/3) % 2 == 0,
                (i, j) => (i * j) % 2 + (i * j) % 3 == 0,
                (i, j) => (((i * j) % 2) +((i * j) % 3) ) % 2 == 0,
               (i, j) => (((i + j) % 2) +((i * j) % 3) ) % 2 == 0,
            };


        /// <summary>
        /// Create a new MyImage representing a QR Code encoding the input string.
        /// </summary>
        /// <remarks>The length of the content can't exceed 154 characters</remarks>
        /// <param name="content">The content of the QR Code as an alphanumeric string</param>
        /// <exception cref="Exception">The string is not alphanumeric</exception>
        public QRCode(string content) : base(0, 0)
        {
            content = content.ToUpper(); 
            if (content.Length > 154) // Version 5 can only contain 154 characters and other versions are not supported
                content = content.Substring(0, 154);
            if (!content.All(x => "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:".Contains(x)))
                throw new Exception("The entered text is not alpha-numeric");


            // The byte corresponding to the mode (alphanumeric)
            byte mode = 0b0010;
            string encodedMode = Convert.ToString(mode, 2).PadLeft(4, '0');
            char correctionLevel = 'L';

            // Find the best version for this content length
            int version = 1;
            while (GetCharacterCapacity(version, correctionLevel, mode) < content.Length)
                version++;

            string encodedContentLength = Convert.ToString(content.Length, 2).PadLeft(9, '0');

            // encodedString will contain the entire encoded data that will be placed in the QR Code
            string encodedString = encodedMode + encodedContentLength + EncodeContent(content);

            int targetNumberOfBits = GetMaxNumberOfBits(version); // The number of bits of the message for this version of QR Code

            // Add up to 4 '0' as a terminator
            if (targetNumberOfBits - encodedString.Length >= 4)
                encodedString += "0000";
            else
                encodedString += new string('0', targetNumberOfBits - encodedString.Length);

            // If number of bits is not a multiple of 8, add bits
            encodedString += new string('0', (8 - (encodedString.Length % 8)) % 8);

            // Add padding bytes
            int switchFlag = 1;
            while (encodedString.Length < targetNumberOfBits)
            {
                if (switchFlag == 1)
                {
                    encodedString += "11101100";
                    switchFlag = 0;
                }
                else
                {
                    encodedString += "00010001";
                    switchFlag = 1;
                }
            }


            // Add the reed solomon bytes
            byte[] encodedReedSolomon = ReedSolomonAlgorithm.Encode(BinaryStringToBytes(encodedString), InformationTables.ErrorCorrectionBytes[version], ErrorCorrectionCodeType.QRCode);

            foreach (byte e in encodedReedSolomon)
            {
                encodedString += Convert.ToString(e, 2).PadLeft(8, '0');
            }

            this.image = new Pixel[21 + (version - 1) * 4, 21 + (version - 1) * 4];

            FillQRCode(this.image, version: version, dataBits: encodedString, correctionLevel: correctionLevel);
        }

        /// <summary>
        /// Gets an image and creates a QR Code object from it, trying to extract a QR Code
        /// </summary>
        /// <param name="image">The image to extract the QR Code from</param>
        public QRCode (MyImage image) : base(0, 0)
        {
            this.image = ExtractQRFromImage(image);
        }

        /// <summary>
        /// Encode an entire alphanumeric to a binary string encoded with the QR Code encoding specifications
        /// </summary>
        /// <param name="content"></param>
        /// <returns>A string of '0' and '1' representing the encoded content</returns>
        public static string EncodeContent(string content)
        {
            string result = "";
            for (int i = 0; i < content.Length; i += 2)
            {
                int encodedValue = 0;
                if (!(i == content.Length - 1)) // We encode the characters two by two
                {
                    encodedValue = EncodeChar(content[i]) * 45 + EncodeChar(content[i + 1]);
                    result += Convert.ToString(encodedValue, 2).PadLeft(11, '0');
                }
                else // If the string hasn't an even number of characters, encode the last character alone
                {
                    encodedValue = EncodeChar(content[i]);
                    result += Convert.ToString(encodedValue, 2).PadLeft(6, '0');
                }
            }

            return result;
        }

        /// <summary>
        /// Get the int corresponding to a char in the QR Code alphanumeric encoding 
        /// </summary>
        /// <param name="character"></param>
        /// <returns></returns>
        public static int EncodeChar(char character)
        {
            if (char.IsDigit(character))
            {
                return Convert.ToInt32(character.ToString());
            }
            else if (char.IsLetter(character))
            {
                return ((int)character) - 55;
            }
            else
            {
                return " $%*+-./:".IndexOf(character) + 36;
            }
        }


        /// <summary>
        /// Get the number of character that can contain a certain version
        /// </summary>
        /// <param name="version"></param>
        /// <param name="correctionLevel"></param>
        /// <param name="mode"></param>
        /// <returns></returns>
        public static int GetCharacterCapacity(int version, char correctionLevel = 'L', byte mode = 0b0010)
        {
            return InformationTables.Capacities[version][correctionLevel][mode];
        }

        /// <summary>
        /// Get the maximum number of bits of the data (correction bits not included) for a certain version
        /// </summary>
        /// <param name="version"></param>
        /// <param name="correctionLevel"></param>
        /// <param name="mode"></param>
        /// <returns></returns>
        public static int GetMaxNumberOfBits(int version, char correctionLevel = 'L', byte mode = 0b0010)
        {
            // Mode indicator + NbChar indicator + Encoded data + End
            int result = 4 + 9 + (int)Math.Ceiling(GetCharacterCapacity(version) * 5.5f);
            result += (8 - (result % 8)) % 8;
            return result;
        }

        /// <summary>
        /// Convert a string of bits to a byte array
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        static byte[] BinaryStringToBytes(string input)
        {
            byte[] result = new byte[input.Length / 8];
            for (int i = 0; i < input.Length / 8; i++)
            {
                result[i] = Convert.ToByte(input.Substring(i * 8, 8), 2);
            }

            return result;
        }


        /// <summary>
        /// Fills a QR Code image with the given data binary string
        /// </summary>
        /// <param name="matrix"></param>
        /// <param name="version"></param>
        /// <param name="dataBits"></param>
        /// <param name="correctionLevel"></param>
        static void FillQRCode(Pixel[,] matrix, int version, string dataBits, char correctionLevel)
        {

            // Reset pixels to a default value (equivalent to null module)
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    matrix[i, j] = new Pixel(127, 0, 0);
                }
            }

            // Separators
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    matrix[i, j] = new Pixel(255, 255, 255); // Top Left
                    matrix[matrix.GetLength(0) - 1 - i, j] = new Pixel(255, 255, 255); // Bottom Left
                    matrix[i, matrix.GetLength(1) - 1 - j] = new Pixel(255, 255, 255); // Top Right
                }
            }

            // Finder Patterns
            for (int i = 0; i < 7; i++)
            {
                for (int j = 0; j < 7; j++)
                {
                    matrix[i, j] = new Pixel(0, 0, 0); // Top Left
                    matrix[matrix.GetLength(0) - 1 - i, j] = new Pixel(0, 0, 0); // Bottom Left
                    matrix[i, matrix.GetLength(1) - 1 - j] = new Pixel(0, 0, 0); // Top Right
                }
            }
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    matrix[i + 1, j + 1] = new Pixel(255, 255, 255); // Top Left
                    matrix[matrix.GetLength(0) - 1 - i - 1, j + 1] = new Pixel(255, 255, 255); // Bottom Left
                    matrix[i + 1, matrix.GetLength(1) - 1 - j - 1] = new Pixel(255, 255, 255); // Top Right
                }
            }
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    matrix[i + 2, j + 2] = new Pixel(0, 0, 0); // Top Left
                    matrix[matrix.GetLength(0) - 1 - i - 2, j + 2] = new Pixel(0, 0, 0); // Bottom Left
                    matrix[i + 2, matrix.GetLength(1) - 1 - j - 2] = new Pixel(0, 0, 0); // Top Right
                }
            }


            // Alignment patterns (Version 2 and above)
            // https://www.thonky.com/qr-code-tutorial/alignment-pattern-locations

            foreach (int row in InformationTables.AlignmentPatternLocations[version])
            {
                foreach (int col in InformationTables.AlignmentPatternLocations[version])
                {
                    if ((row >= 10 && col >= 10) || (row >= 10 && row <= matrix.GetLength(0) - 11) || (col >= 10 && col <= matrix.GetLength(0) - 11))
                    {
                        for (int i = row - 2; i <= row + 2; i++)
                        {
                            for (int j = col - 2; j <= col + 2; j++)
                            {
                                matrix[i, j] = new Pixel(0, 0, 0);
                            }
                        }

                        for (int i = row - 1; i <= row + 1; i++)
                        {
                            for (int j = col - 1; j <= col + 1; j++)
                            {
                                matrix[i, j] = new Pixel(255, 255, 255);
                            }
                        }

                        matrix[row, col] = new Pixel(0, 0, 0);
                    }
                }
            }


            // Horizontal timing
            for (int i = 8; i <= matrix.GetLength(1) - 9; i++)
            {
                matrix[6, i] = i % 2 == 0 ? new Pixel(0, 0, 0) : new Pixel(255, 255, 255);
            }


            // Vertical timing
            for (int i = 8; i <= matrix.GetLength(0) - 9; i++)
            {
                matrix[i, 6] = i % 2 == 0 ? new Pixel(0, 0, 0) : new Pixel(255, 255, 255);
            }


            // Dark module
            matrix[version * 4 + 9, 8] = new Pixel(0, 0, 0);


            // Reserved Areas

            // Format information area
            for (int i = 0; i <= 8; i++)
            {
                // Top left
                if (matrix[8, i].R == 127)
                    matrix[8, i] = new Pixel(0, 0, 127);
                if (matrix[i, 8].R == 127)
                    matrix[i, 8] = new Pixel(0, 0, 127);

                if (i != 8)
                {
                    // Top right
                    if (matrix[8, matrix.GetLength(1) - 1 - i].R == 127)
                        matrix[8, matrix.GetLength(1) - 1 - i] = new Pixel(0, 0, 127);

                    // Bottom left
                    if (matrix[matrix.GetLength(1) - 1 - i, 8].R == 127)
                        matrix[matrix.GetLength(1) - 1 - i, 8] = new Pixel(0, 0, 127);
                }
            }

            // Version information area (Version 7 and above)

            int maskIndex = 0;

            // Data bits placement
            Pixel[,] dataMatrix = new Pixel[matrix.GetLength(0), matrix.GetLength(1)]; // Only in use if we want to use an optimized mask


            int offset = 0;

            int line = matrix.GetLength(0) - 1;
            int column = matrix.GetLength(1) - 1;
            bool up = true;

            while (column >= 1)
            {

                for (int k = 0; k < 2; k++)
                {
                    // Check if module has default value (it is not reserved)
                    if (matrix[line, column - k].R == 127)
                    {
                        int bit;
                        if (offset >= dataBits.Length)
                            bit = 0;
                        else
                            bit = int.Parse(dataBits[offset].ToString());
                        if (masks[maskIndex](line, column - k))
                            bit = 1 - bit;

                        matrix[line, column - k] = new Pixel((1 - bit) * 255, (1 - bit) * 255, (1 - bit) * 255);

                        offset++;
                    }
                }

                if (up)
                {

                    if (line == 0)
                    {
                        column -= 2;
                        up = !up;
                    }
                    else
                    {
                        line--;
                    }
                }
                else
                {
                    if (line == matrix.GetLength(0) - 1)
                    {
                        column -= 2;
                        up = !up;
                    }
                    else
                    {
                        line++;
                    }
                }

                // Skip vertical timing
                if (column == 6)
                    column = 5;
            }

            // Mask patterns https://www.thonky.com/qr-code-tutorial/mask-patterns


            // Format string
            string formatString = "";

            switch (correctionLevel)
            {
                case 'L':
                    formatString += "01";
                    break;

                case 'M':
                    formatString += "00";
                    break;

                case 'Q':
                    formatString += "11";
                    break;

                case 'H':
                    formatString += "10";
                    break;
            }

            formatString += Convert.ToString(maskIndex, 2).PadLeft(3, '0');


            // Error correction bits

            string correctionString = formatString.PadRight(15, '0');
            correctionString = correctionString.TrimStart('0');
            string generatorPolynomial = "10100110111";


            do
            {
                correctionString = PolynomialDivision(correctionString, generatorPolynomial);
            } while (correctionString.Length > 10);

            formatString += correctionString;

            formatString = Convert.ToString(Convert.ToInt32(formatString, 2) ^ Convert.ToInt32("101010000010010", 2), 2).PadLeft(formatString.Length, '0');


            // Put format string into QR Code

            offset = 0;
            for (int j = 0; j <= 8; j++)
            {
                if (matrix[8, j].B == 127)
                {
                    int bit = Convert.ToInt32(formatString[offset].ToString());
                    matrix[8, j] = new Pixel((1 - bit) * 255, (1 - bit) * 255, (1 - bit) * 255);
                    offset++;
                }
            }
            for (int i = 8; i >= 0; i--)
            {
                if (matrix[i, 8].B == 127)
                {
                    int bit = Convert.ToInt32(formatString[offset].ToString());
                    matrix[i, 8] = new Pixel((1 - bit) * 255, (1 - bit) * 255, (1 - bit) * 255);
                    offset++;
                }
            }


            offset = 0;
            for (int i = matrix.GetLength(0) - 1; i >= matrix.GetLength(0) - 1 - 7; i--)
            {
                if (matrix[i, 8].B == 127)
                {
                    int bit = Convert.ToInt32(formatString[offset].ToString());
                    matrix[i, 8] = new Pixel((1 - bit) * 255, (1 - bit) * 255, (1 - bit) * 255);
                    offset++;
                }
            }

            for (int j = matrix.GetLength(1) - 1 - 7; j <= matrix.GetLength(1) - 1; j++)
            {
                if (matrix[8, j].B == 127)
                {
                    int bit = Convert.ToInt32(formatString[offset].ToString());
                    matrix[8, j] = new Pixel((1 - bit) * 255, (1 - bit) * 255, (1 - bit) * 255);
                    offset++;
                }
            }
        }

        /// <summary>
        /// Extract the encoded data as a text string from the QR Code
        /// </summary>
        /// <returns></returns>
        public string Read()
        {
            if (this.image == null) // No QR Code found
                return null;

            Pixel[,] matrix = this.image;

            int version = (matrix.GetLength(0) - 21)/4 + 1;
            Console.WriteLine("\nVersion: " + version);

            (char error, int mask) = GetFormatInformations(matrix);

            Console.WriteLine("Mask: " + mask + " Error: " + error);

            // Set the reserved areas in blue (0, 0, 127)

            // Separators
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    matrix[i, j] = new Pixel(0, 0, 127); // Top Left
                    matrix[matrix.GetLength(0) - 1 - i, j] = new Pixel(0, 0, 127); // Bottom Left
                    matrix[i, matrix.GetLength(1) - 1 - j] = new Pixel(0, 0, 127); // Top Right
                }
            }


            // Alignment patterns (Version 2 and above)
            // https://www.thonky.com/qr-code-tutorial/alignment-pattern-locations

            foreach (int row in InformationTables.AlignmentPatternLocations[version])
            {
                foreach (int col in InformationTables.AlignmentPatternLocations[version])
                {
                    if ((row >= 10 && col >= 10) || (row >= 10 && row <= matrix.GetLength(0) - 11) || (col >= 10 && col <= matrix.GetLength(0) - 11))
                    {
                        for (int i = row - 2; i <= row + 2; i++)
                        {
                            for (int j = col - 2; j <= col + 2; j++)
                            {
                                matrix[i, j] = new Pixel(0, 0, 127);
                            }
                        }
                    }
                }
            }


            // Horizontal timing
            for (int i = 8; i <= matrix.GetLength(1) - 9; i++)
            {
                matrix[6, i] = new Pixel(0, 0, 127);
            }

            // Vertical timing
            for (int i = 8; i <= matrix.GetLength(0) - 9; i++)
            {
                matrix[i, 6] = new Pixel(0, 0, 127);
            }


            // Dark module
            matrix[version * 4 + 9, 8] = new Pixel(0, 0, 127);


            // Reserved Areas

            // Format information area
            for (int i = 0; i <= 8; i++)
            {
                // Top left
                matrix[8, i] = new Pixel(0, 0, 127);
                matrix[i, 8] = new Pixel(0, 0, 127);

                if (i != 8)
                {
                    // Top right
                    matrix[8, matrix.GetLength(1) - 1 - i] = new Pixel(0, 0, 127);

                    // Bottom left
                    matrix[matrix.GetLength(1) - 1 - i, 8] = new Pixel(0, 0, 127);
                }
            }

            // Version information area (Version 7 and above)



            int line = matrix.GetLength(0) - 1;
            int column = matrix.GetLength(1) - 1;
            bool up = true;

            string databits = "";


            // Get the databits in the matrix
            while (column >= 1)
            {

                for (int k = 0; k < 2; k++)
                {
                    // Check if module has default value (it is not reserved)
                    if (matrix[line, column - k].B != 127)
                    {
                        int bit = matrix[line, column - k].R == 0 ? 1 : 0;

                        if (masks[mask](line, column - k))
                            bit = 1 - bit;

                        databits += Convert.ToString(bit, 2).PadLeft(1, '0');
                    }
                }

                if (up)
                {

                    if (line == 0)
                    {
                        column -= 2;
                        up = !up;
                    }
                    else
                    {
                        line--;
                    }
                }
                else
                {
                    if (line == matrix.GetLength(0) - 1)
                    {
                        column -= 2;
                        up = !up;
                    }
                    else
                    {
                        line++;
                    }
                }

                // Skip vertical timing
                if (column == 7 || column == 6)
                    column = 5;
            }

            return DecodeData(databits, version);
        }

        /// <summary>
        /// Extract only the matrix containing the QR Code from an image (with module size of 1 pixel)
        /// </summary>
        /// <returns>The matrix of the QR Code</returns>
        static Pixel[,] ExtractQRFromImage(MyImage image)
        {
            Pixel[,] matrix = image.image;

            int moduleSize; // Size of a module in pixels (only integers)
            int threshold = 16; // Threshold for pixel to be considered as black (under 10) or white (above 245)

            int size = matrix.GetLength(0); // The size in pixels of the QR Code
            (int, int) topLeft = (0, 0); // Line and column of the top left pixel of the QR Code
            (int, int) bottomRight = (matrix.GetLength(0) - 1, matrix.GetLength(1) - 1);

            // Find the Finder patterns
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    if (matrix[i, j].Max < threshold) // Found a black pixel
                    {
                        
                        int length = 0;

                        {
                            int j2 = j;
                            // Get the length of the black line
                            while (matrix[i, j2].Max < threshold)
                            {
                                j2++;
                                length++;
                            }
                        }
                        

                        // We suppose black line is the Finder pattern, then module size is black line length / 7
                        moduleSize = length / 7;

                        bool validFinderPattern = true;

                        if (moduleSize == 0)
                            validFinderPattern = false;

                        if (validFinderPattern)
                        {
                            // We check if it's really a finder pattern
                            for (int i2 = i; i2 < 7; i2 += moduleSize)
                            {
                                if (matrix[i2, j].Max > threshold) // The pixel is not black
                                {
                                    validFinderPattern = false;
                                    break;
                                }
                            }

                            for (int i2 = i + moduleSize; i2 < 5; i2 += moduleSize)
                            {
                                if (matrix[i2, j+moduleSize].Min < 255 - threshold) // The pixel is not white
                                {
                                    validFinderPattern = false;
                                    break;
                                }
                            }
                        }

                        if (validFinderPattern)
                        {
                            // If so, top left pixel is (i, j)
                            topLeft = (i, j);

                            // Get the size of the QR Code
                            // Find the next horizontal black line of 7 pixels
                            int blackPixelsLineLength = 0;
                            for (int j2 = topLeft.Item2 + 7*moduleSize; j2 < matrix.GetLength(1); j2 += moduleSize)
                            {
                                if (matrix[topLeft.Item1, j2].Max < threshold) // Found black pixel
                                {
                                    blackPixelsLineLength++;
                                }
                                else if (matrix[topLeft.Item1, j2].Min > 255 - threshold) // Found white pixel
                                {
                                    blackPixelsLineLength = 0;
                                }
                                else
                                {
                                    // Pixel is neither white nor black
                                    validFinderPattern = false;
                                    break;
                                }
                                
                                if (blackPixelsLineLength == 7) // We probably found the top right finder pattern
                                {
                                    size = j2 - topLeft.Item2 + moduleSize;
                                    bottomRight = (topLeft.Item1 + size - 1, topLeft.Item2 + size - 1);
                                    break;

                                }
                            }
                        }

                        if (validFinderPattern)
                            goto Extract;
                    }
                }
            }

            // QR Code not found
            return null;

        Extract:

            Pixel[,] result = new Pixel[(int) Math.Round((double) size / moduleSize), (int)Math.Round((double) size / moduleSize)];
            {
                int i2 = 0;
                for (int i = topLeft.Item1 + moduleSize / 2; i <= bottomRight.Item1; i += moduleSize)
                {
                    int j2 = 0;
                    for (int j = topLeft.Item2 + moduleSize / 2; j <= bottomRight.Item2; j += moduleSize)
                    {

                        if (matrix[i, j].Max < 127)
                        {
                            result[i2, j2] = new Pixel(0, 0, 0);
                        }
                        else
                        {
                            result[i2, j2] = new Pixel(255, 255, 255);
                        }

                        j2++;
                    }
                    i2++;
                }
            }

            return result;
        }

        /// <summary>
        /// Decode the encoded alphanumeric text from a string of bits
        /// </summary>
        /// <param name="databits"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static string DecodeData(string databits, int version)
        {
            string message = databits.Substring(0, GetMaxNumberOfBits(version));
            string ecc = databits.Substring(GetMaxNumberOfBits(version), (databits.Length - message.Length) - databits.Length%8);

            
            byte[] decodedMessage = ReedSolomonAlgorithm.Decode(BinaryStringToBytes(message), BinaryStringToBytes(ecc), ErrorCorrectionCodeType.QRCode);

            string messageBits = "";

            foreach (byte e in decodedMessage)
            {
                messageBits += Convert.ToString(e, 2).PadLeft(8, '0');
            }
            


            byte mode = Convert.ToByte(databits.Substring(0, 4), 2);
            int characterCount = 0;

            string result = "";

            if (mode == 0b0010)
            {
                // Alphanumeric mode
                if (version <= 9)
                {
                    characterCount = Convert.ToInt32(databits.Substring(4, 9), 2);
                    messageBits = messageBits.Substring(13);
                }
                else
                {
                    throw new Exception("Version too big");
                }

                for (int i = 0; i < (characterCount)/2; i++)
                {
                    int code = Convert.ToInt32(messageBits.Substring(i*11, 11), 2);
                    result += DecodeChar((int) (code / 45));
                    result += DecodeChar(code % 45);
                }
                if (characterCount % 2 != 0)
                {
                    int code = Convert.ToInt32(messageBits.Substring((characterCount / 2) * 11, 6), 2);
                    result += DecodeChar(code);
                }
            }
            else if (mode == 0b0100)
            {
                // Bytes mode
                if (version <= 9)
                {
                    characterCount = Convert.ToInt32(databits.Substring(4, 8), 2);
                    messageBits = messageBits.Substring(12);
                }
                else
                {
                    throw new Exception("Version too big");
                }

                for (int i = 0; i < characterCount; i++)
                {
                    result += (char) Convert.ToByte(messageBits.Substring(i * 8, 8), 2);
                }
            }
            else
            {

                throw new Exception("Mode not accepted: " + Convert.ToString(mode, 2).PadLeft(4, '0'));
            }

            return result;
        }

        /// <summary>
        /// Get the char corresponding to a certain code according to the QR Code alphanumeric encoding
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public static char DecodeChar (int code)
        {
            if (code <= 9)
            {
                return code.ToString()[0];
            }
            else if (code <= 35)
            {
                return (char)(65 + code - 10);
            }
            else
            {
                return " $%*+-./:"[code - 36];
            }
        }

        /// <summary>
        /// Get the error correction level and mask id
        /// </summary>
        /// <param name="qr_matrix"></param>
        /// <returns></returns>
        static (char, int) GetFormatInformations (Pixel[,] qr_matrix)
        {
            string errorCorrectionString = "";
            string maskString = "";

            for (int j = 0; j < 2; j++)
            {
                errorCorrectionString += (1 - (qr_matrix[8, j].Max / 255)).ToString().PadLeft(1, '0');
            }
            for (int j = 2; j < 5; j++)
            {
                maskString += (1 - (qr_matrix[8, j].Max / 255)).ToString().PadLeft(1, '0');
            }

            char errorCorrectionLevel;
            switch(Convert.ToInt32(errorCorrectionString, 2) ^ 0b10)
            {
                default:
                case 1:
                    errorCorrectionLevel = 'L';
                    break;

                case 0:
                    errorCorrectionLevel = 'M';
                    break;

                case 3:
                    errorCorrectionLevel = 'Q';
                    break;

                case 2:
                    errorCorrectionLevel = 'H';
                    break;
            }

            return (errorCorrectionLevel, Convert.ToInt32(maskString, 2) ^ 0b101);
        }

        /// <summary>
        /// Divide to strings of bits in GF(256)
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        static string PolynomialDivision(string a, string b)
        {
            // Step 1
            b = b.PadRight(a.Length, '0');

            // Step 2
            string c = Convert.ToString(Convert.ToInt32(a, 2) ^ Convert.ToInt32(b, 2), 2).PadLeft(a.Length, '0');

            // Step 3
            c = c.TrimStart('0');

            return c;
        }
    }
}
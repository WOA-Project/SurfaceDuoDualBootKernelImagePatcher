namespace SurfaceDuoDualBootKernelImagePatcher
{
    internal enum SurfaceDuoProduct
    {
        Epsilon,
        Zeta
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Surface Duo Dual Boot Kernel Image Patcher v2.0.0.0");
            Console.WriteLine("Copyright (c) 2021-2023 The DuoWoA authors");
            Console.WriteLine();

            if (args.Length != 4)
            {
                Console.WriteLine("Usage: <Kernel Image to Patch> <UEFI FD Image> <0: Epsilon, 1: Zeta> <Patched Kernel Image Destination>");
                return;
            }

            string originalImage = args[0];
            string uefiImage = args[1];
            SurfaceDuoProduct product = (SurfaceDuoProduct)uint.Parse(args[2]);
            string outputImage = args[3];

            Console.WriteLine($"Patching {originalImage} with {uefiImage} for {product} and saving to {outputImage}...");
            Console.WriteLine();

            File.WriteAllBytes(outputImage, PatchKernel(File.ReadAllBytes(originalImage), File.ReadAllBytes(uefiImage), product));

            Console.WriteLine("Image successfully patched.");
            Console.WriteLine($"Please find the newly made kernel image at {outputImage}");
        }

        static byte[] PatchKernel(byte[] kernelBuffer, byte[] uefiBuffer, SurfaceDuoProduct surfaceDuoProduct)
        {
            byte[] patchedKernelBuffer = new byte[kernelBuffer.Length + uefiBuffer.Length];

            // Copy the original kernel first into the patched buffer
            Array.Copy(kernelBuffer, patchedKernelBuffer, kernelBuffer.Length);
            Array.Copy(uefiBuffer, 0, patchedKernelBuffer, kernelBuffer.Length, uefiBuffer.Length);

            // Determine the loading offset of the kernel first,
            // we are either going to find a b instruction on the
            // first instruction or the second one. First is problematic,
            // second is fine.

            // 0x14 is AArch64 b opcode
            if (patchedKernelBuffer[3] == 0x14)
            {
                // We have a branch instruction first, we need to fix things a bit.

                // First start by getting the actual value of the branch addr offset.

                byte[] offsetInstructionBuffer = { patchedKernelBuffer[0], patchedKernelBuffer[1], patchedKernelBuffer[2], 0 };
                uint offsetInstruction = BitConverter.ToUInt32(offsetInstructionBuffer);

                // Now substract 1 instruction because we'll move this to the second instruction of the kernel header.
                offsetInstruction--;

                // Convert back into an actual value that is usable as a b instruction
                offsetInstructionBuffer = BitConverter.GetBytes(offsetInstruction);
                offsetInstructionBuffer[3] = 0x14; // Useless but just for our sanity :)

                // Now write the instruction back into the kernel (instr 2)
                patchedKernelBuffer[4] = offsetInstructionBuffer[0];
                patchedKernelBuffer[5] = offsetInstructionBuffer[1];
                patchedKernelBuffer[6] = offsetInstructionBuffer[2];
                patchedKernelBuffer[7] = 0x14;
            }
            else if (patchedKernelBuffer[7] != 0x14)
            {
                // There is no branch instruction!
                throw new Exception("Invalid Kernel Image. Branch instruction not found within first two instruction slots.");
            }

            // Alright, our kernel image has a compatible branch instruction, let's start.

            // First, add the jump to our code on instr 1
            // This directly jump right after the kernel header (there's enough headroom here)
            patchedKernelBuffer[0] = 0x10;
            patchedKernelBuffer[1] = 0;
            patchedKernelBuffer[2] = 0;
            patchedKernelBuffer[3] = 0x14;

            if (surfaceDuoProduct == SurfaceDuoProduct.Epsilon)
            {
                // Now we need to fill in the stack base of our firmware
                // Stack Base: 0x00000000 9FC00000 (64 bit!)
                patchedKernelBuffer[0x20] = 0;
                patchedKernelBuffer[0x21] = 0;
                patchedKernelBuffer[0x22] = 0xC0;
                patchedKernelBuffer[0x23] = 0x9F;
                patchedKernelBuffer[0x24] = 0;
                patchedKernelBuffer[0x25] = 0;
                patchedKernelBuffer[0x26] = 0;
                patchedKernelBuffer[0x27] = 0;

                // Then we need to fill in the stack size of our firmware
                // Stack Base: 0x00000000 00300000 (64 bit!)
                patchedKernelBuffer[0x28] = 0;
                patchedKernelBuffer[0x29] = 0;
                patchedKernelBuffer[0x2A] = 0x30;
                patchedKernelBuffer[0x2B] = 0;
                patchedKernelBuffer[0x2C] = 0;
                patchedKernelBuffer[0x2D] = 0;
                patchedKernelBuffer[0x2E] = 0;
                patchedKernelBuffer[0x2F] = 0;
            }
            else if (surfaceDuoProduct == SurfaceDuoProduct.Zeta)
            {
                // Now we need to fill in the stack base of our firmware
                // Stack Base: 0x00000000 9FC41000 (64 bit!)
                patchedKernelBuffer[0x20] = 0;
                patchedKernelBuffer[0x21] = 0x10;
                patchedKernelBuffer[0x22] = 0xC4;
                patchedKernelBuffer[0x23] = 0x9F;
                patchedKernelBuffer[0x24] = 0;
                patchedKernelBuffer[0x25] = 0;
                patchedKernelBuffer[0x26] = 0;
                patchedKernelBuffer[0x27] = 0;

                // Then we need to fill in the stack size of our firmware
                // Stack Base: 0x00000000 002BF000 (64 bit!)
                patchedKernelBuffer[0x28] = 0;
                patchedKernelBuffer[0x29] = 0xF0;
                patchedKernelBuffer[0x2A] = 0x2B;
                patchedKernelBuffer[0x2B] = 0;
                patchedKernelBuffer[0x2C] = 0;
                patchedKernelBuffer[0x2D] = 0;
                patchedKernelBuffer[0x2E] = 0;
                patchedKernelBuffer[0x2F] = 0;
            }
            else
            {
                throw new Exception("Unknown Surface Duo Product specified!");
            }

            // Finally, we add in the total kernel image size because we need to jump over!
            uint kernelSize = (uint)kernelBuffer.Length;
            byte[] kernelSizeBuffer = BitConverter.GetBytes(kernelSize);
            patchedKernelBuffer[0x30] = kernelSizeBuffer[0];
            patchedKernelBuffer[0x31] = kernelSizeBuffer[1];
            patchedKernelBuffer[0x32] = kernelSizeBuffer[2];
            patchedKernelBuffer[0x33] = kernelSizeBuffer[3];
            patchedKernelBuffer[0x34] = 0;
            patchedKernelBuffer[0x35] = 0;
            patchedKernelBuffer[0x36] = 0;
            patchedKernelBuffer[0x37] = 0;

            // Now our header is fully patched, let's add a tiny bit
            // of code as well to decide what to do.

            byte[] epsilonShellCode = new byte[]
            {
                0x84, 0x00, 0x92, 0xD2, 0xE4, 0x7A, 0xA0, 0xF2, 0x85, 0x00, 0x40, 0xB9, 0xA5, 0x00, 0x00, 0x12, 0x45, 0x00, 0x00, 0x34, 0xEC, 0xFF, 0xFF, 0x17, 0x44, 0xFD, 0xFF, 0x10, 0xA5, 0xFE, 0xFF, 0x58, 0x84, 0x00, 0x05, 0x8B, 0xE5, 0xFD, 0xFF, 0x58, 0x06, 0xFE, 0xFF, 0x58, 0x05, 0x00, 0x00, 0x94, 0x80, 0x01, 0x00, 0x10, 0x61, 0x01, 0x00, 0x10, 0x45, 0xFD, 0xFF, 0x58, 0xA0, 0x00, 0x1F, 0xD6, 0x82, 0x0C, 0xC1, 0xA8, 0xA2, 0x0C, 0x81, 0xA8, 0xC6, 0x40, 0x00, 0xF1, 0xA1, 0xFF, 0xFF, 0x54, 0xC0, 0x03, 0x5F, 0xD6, 0x00, 0x00, 0x00, 0x14, 0x1F, 0x20, 0x03, 0xD5, 0x1F, 0x20, 0x03, 0xD5
            };

            byte[] zetaShellCode = new byte[]
            {
                0x84, 0x00, 0x80, 0xD2, 0x04, 0xE2, 0xA1, 0xF2, 0x85, 0x00, 0x40, 0xB9, 0xA5, 0x00, 0x00, 0x12, 0x45, 0x00, 0x00, 0x35, 0xEC, 0xFF, 0xFF, 0x17, 0x44, 0xFD, 0xFF, 0x10, 0xA5, 0xFE, 0xFF, 0x58, 0x84, 0x00, 0x05, 0x8B, 0xE5, 0xFD, 0xFF, 0x58, 0x06, 0xFE, 0xFF, 0x58, 0x05, 0x00, 0x00, 0x94, 0x80, 0x01, 0x00, 0x10, 0x61, 0x01, 0x00, 0x10, 0x45, 0xFD, 0xFF, 0x58, 0xA0, 0x00, 0x1F, 0xD6, 0x82, 0x0C, 0xC1, 0xA8, 0xA2, 0x0C, 0x81, 0xA8, 0xC6, 0x40, 0x00, 0xF1, 0xA1, 0xFF, 0xFF, 0x54, 0xC0, 0x03, 0x5F, 0xD6, 0x00, 0x00, 0x00, 0x14, 0x1F, 0x20, 0x03, 0xD5, 0x1F, 0x20, 0x03, 0xD5
            };

            if (surfaceDuoProduct == SurfaceDuoProduct.Epsilon)
            {
                Array.Copy(epsilonShellCode, 0, patchedKernelBuffer, 0x40, epsilonShellCode.Length);
            }
            else if (surfaceDuoProduct == SurfaceDuoProduct.Zeta)
            {
                Array.Copy(zetaShellCode, 0, patchedKernelBuffer, 0x40, zetaShellCode.Length);
            }
            else
            {
                throw new Exception("Unknown Surface Duo Product specified!");
            }

            // And that's it, the user now can append executable code right after the kernel,
            // and upon closing up the device said code will run at boot. Have fun!

            return patchedKernelBuffer;
        }
    }
}
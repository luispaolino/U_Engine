public static class SimTestHarness
{
    public static int RunFrames(
        FighterCharacterCore atk,
        FighterCharacterCore def,
        int frames,
        System.Func<int,bool> perFrameAssert = null)
    {
        for (int f = 0; f < frames; f++)
        {
            atk.FixedTick();
            def.FixedTick();
            if (perFrameAssert != null && !perFrameAssert(f))
                throw new System.Exception($"Assertion failed at frame {f}");
        }
        return frames;
    }
}

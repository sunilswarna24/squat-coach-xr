namespace SquatCoach.Analysis
{
    /// <summary>
    /// Named indices into a MediaPipe pose landmark list. Matches the Python
    /// definition in pi/src/pose_types.py exactly — the two must stay in sync.
    /// </summary>
    public static class LM
    {
        public const int Nose = 0;
        public const int LeftEyeInner = 1;
        public const int LeftEye = 2;
        public const int LeftEyeOuter = 3;
        public const int RightEyeInner = 4;
        public const int RightEye = 5;
        public const int RightEyeOuter = 6;
        public const int LeftEar = 7;
        public const int RightEar = 8;
        public const int MouthLeft = 9;
        public const int MouthRight = 10;
        public const int LeftShoulder = 11;
        public const int RightShoulder = 12;
        public const int LeftElbow = 13;
        public const int RightElbow = 14;
        public const int LeftWrist = 15;
        public const int RightWrist = 16;
        public const int LeftPinky = 17;
        public const int RightPinky = 18;
        public const int LeftIndex = 19;
        public const int RightIndex = 20;
        public const int LeftThumb = 21;
        public const int RightThumb = 22;
        public const int LeftHip = 23;
        public const int RightHip = 24;
        public const int LeftKnee = 25;
        public const int RightKnee = 26;
        public const int LeftAnkle = 27;
        public const int RightAnkle = 28;
        public const int LeftHeel = 29;
        public const int RightHeel = 30;
        public const int LeftFootIndex = 31;
        public const int RightFootIndex = 32;

        public const int Count = 33;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AssetStudio
{
    public class Keyframe<T> : IYAMLExportable where T : IYAMLExportable
    {
        public float time;
        public T value;
        public T inSlope;
        public T outSlope;
        public int weightedMode;
        public T inWeight;
        public T outWeight;

        public Keyframe(float time, T value, T inSlope, T outSlope, T weight)
        {
            this.time = time;
            this.value = value;
            this.inSlope = inSlope;
            this.outSlope = outSlope;
            weightedMode = 0;
            inWeight = weight;
            outWeight = weight;
        }

        public Keyframe(ObjectReader reader, Func<T> readerFunc)
        {
            time = reader.ReadSingle();
            value = readerFunc();
            inSlope = readerFunc();
            outSlope = readerFunc();
            if (reader.version[0] >= 2018) //2018 and up
            {
                weightedMode = reader.ReadInt32();
                inWeight = readerFunc();
                outWeight = readerFunc();
            }
        }

        public YAMLNode ExportYAML(int[] version)
        {
            var node = new YAMLMappingNode();
            node.AddSerializedVersion(ToSerializedVersion(version));
            node.Add(nameof(time), time);
            node.Add(nameof(value), value.ExportYAML(version));
            node.Add(nameof(inSlope), inSlope.ExportYAML(version));
            node.Add(nameof(outSlope), outSlope.ExportYAML(version));
            if (version[0] >= 2018) //2018 and up
            {
                node.Add(nameof(weightedMode), weightedMode);
                node.Add(nameof(inWeight), inWeight.ExportYAML(version));
                node.Add(nameof(outWeight), outWeight.ExportYAML(version));
            }
            return node;
        }

        private int ToSerializedVersion(int[] version)
        {
            if (version[0] >= 2018) //2018 and up
            {
                return 3;
            }
            else if (version[0] > 5 || (version[0] == 5 && version[1] >= 5))
            {
                return 2;
            }
            return 1;
        }
    }

    public class AnimationCurve<T> : IYAMLExportable where T : IYAMLExportable
    {
        public List<Keyframe<T>> m_Curve;
        public int m_PreInfinity;
        public int m_PostInfinity;
        public int m_RotationOrder;

        public AnimationCurve()
        {
            m_PreInfinity = 2;
            m_PostInfinity = 2;
            m_RotationOrder = 4;
            m_Curve = new List<Keyframe<T>>();
        }

        public AnimationCurve(ObjectReader reader, Func<T> readerFunc)
        {
            var version = reader.version;
            int numCurves = reader.ReadInt32();
            m_Curve = new List<Keyframe<T>>();
            for (int i = 0; i < numCurves; i++)
            {
                m_Curve.Add(new Keyframe<T>(reader, readerFunc));
            }

            m_PreInfinity = reader.ReadInt32();
            m_PostInfinity = reader.ReadInt32();
            if (version[0] > 5 || (version[0] == 5 && version[1] >= 3))//5.3 and up
            {
                m_RotationOrder = reader.ReadInt32();
            }
        }

        public YAMLNode ExportYAML(int[] version)
        {
            var node = new YAMLMappingNode();
            node.AddSerializedVersion(ToSerializedVersion(version));
            node.Add(nameof(m_Curve), m_Curve.ExportYAML(version));
            node.Add(nameof(m_PreInfinity), m_PreInfinity);
            node.Add(nameof(m_PostInfinity), m_PostInfinity);
            if (version[0] > 5 || (version[0] == 5 && version[1] >= 3))//5.3 and up
            {
                node.Add(nameof(m_RotationOrder), m_RotationOrder);
            }
            return node;
        }

        private int ToSerializedVersion(int[] version)
        {
            if (version[0] > 2 || (version[0] == 2 && version[1] >= 1))
            {
                return 2;
            }
            return 1;
        }
    }

    public class QuaternionCurve : IYAMLExportable
    {
        public AnimationCurve<Quaternion> curve;
        public string path;

        public QuaternionCurve(string path)
        {
            curve = new AnimationCurve<Quaternion>();
            this.path = path;
        }

        public QuaternionCurve(ObjectReader reader)
        {
            curve = new AnimationCurve<Quaternion>(reader, reader.ReadQuaternion);
            path = reader.ReadAlignedString();
        }

        public YAMLNode ExportYAML(int[] version)
        {
            YAMLMappingNode node = new YAMLMappingNode();
            node.Add(nameof(curve), curve.ExportYAML(version));
            node.Add(nameof(path), path);
            return node;
        }
        public override bool Equals(object obj)
        {
            if (obj is QuaternionCurve quaternionCurve)
            {
                return path == quaternionCurve.path;
            }
            return false;
        }

        public override int GetHashCode()
        {
            int hash = 199;
            unchecked
            {
                hash = 617 + hash * path.GetHashCode();
            }
            return hash;
        }
    }

    public class PackedFloatVector : IYAMLExportable
    {
        public uint m_NumItems;
        public float m_Range;
        public float m_Start;
        public byte[] m_Data;
        public byte m_BitSize;

        public PackedFloatVector(ObjectReader reader)
        {
            m_NumItems = reader.ReadUInt32();
            m_Range = reader.ReadSingle();
            m_Start = reader.ReadSingle();

            int numData = reader.ReadInt32();
            m_Data = reader.ReadBytes(numData);
            reader.AlignStream();

            m_BitSize = reader.ReadByte();
            reader.AlignStream();
        }

        public YAMLNode ExportYAML(int[] version)
        {
            var node = new YAMLMappingNode();
            node.Add(nameof(m_NumItems), m_NumItems);
            node.Add(nameof(m_Range), m_Range);
            node.Add(nameof(m_Start), m_Start);
            node.Add(nameof(m_Data), m_Data.ExportYAML());
            node.Add(nameof(m_BitSize), m_BitSize);
            return node;
        }

        public float[] UnpackFloats(int itemCountInChunk, int chunkStride, int start = 0, int numChunks = -1)
        {
            int bitPos = m_BitSize * start;
            int indexPos = bitPos / 8;
            bitPos %= 8;

            float scale = 1.0f / m_Range;
            if (numChunks == -1)
                numChunks = (int)m_NumItems / itemCountInChunk;
            var end = chunkStride * numChunks / 4;
            var data = new List<float>();
            for (var index = 0; index != end; index += chunkStride / 4)
            {
                for (int i = 0; i < itemCountInChunk; ++i)
                {
                    uint x = 0;

                    int bits = 0;
                    while (bits < m_BitSize)
                    {
                        x |= (uint)((m_Data[indexPos] >> bitPos) << bits);
                        int num = Math.Min(m_BitSize - bits, 8 - bitPos);
                        bitPos += num;
                        bits += num;
                        if (bitPos == 8)
                        {
                            indexPos++;
                            bitPos = 0;
                        }
                    }
                    x &= (uint)(1 << m_BitSize) - 1u;
                    data.Add(x / (scale * ((1 << m_BitSize) - 1)) + m_Start);
                }
            }

            return data.ToArray();
        }
    }

    public class PackedIntVector : IYAMLExportable
    {
        public uint m_NumItems;
        public byte[] m_Data;
        public byte m_BitSize;

        public PackedIntVector(ObjectReader reader)
        {
            m_NumItems = reader.ReadUInt32();

            int numData = reader.ReadInt32();
            m_Data = reader.ReadBytes(numData);
            reader.AlignStream();

            m_BitSize = reader.ReadByte();
            reader.AlignStream();
        }
        public YAMLNode ExportYAML(int[] version)
        {
            var node = new YAMLMappingNode();
            node.Add(nameof(m_NumItems), m_NumItems);
            node.Add(nameof(m_Data), m_Data.ExportYAML());
            node.Add(nameof(m_BitSize), m_BitSize);
            return node;
        }

        public int[] UnpackInts()
        {
            var data = new int[m_NumItems];
            int indexPos = 0;
            int bitPos = 0;
            for (int i = 0; i < m_NumItems; i++)
            {
                int bits = 0;
                data[i] = 0;
                while (bits < m_BitSize)
                {
                    data[i] |= (m_Data[indexPos] >> bitPos) << bits;
                    int num = Math.Min(m_BitSize - bits, 8 - bitPos);
                    bitPos += num;
                    bits += num;
                    if (bitPos == 8)
                    {
                        indexPos++;
                        bitPos = 0;
                    }
                }
                data[i] &= (1 << m_BitSize) - 1;
            }
            return data;
        }
    }

    public class PackedQuatVector : IYAMLExportable
    {
        public uint m_NumItems;
        public byte[] m_Data;

        public PackedQuatVector(ObjectReader reader)
        {
            m_NumItems = reader.ReadUInt32();

            int numData = reader.ReadInt32();
            m_Data = reader.ReadBytes(numData);

            reader.AlignStream();
        }

        public YAMLNode ExportYAML(int[] version)
        {
            var node = new YAMLMappingNode();
            node.Add(nameof(m_NumItems), m_NumItems);
            node.Add(nameof(m_Data), m_Data.ExportYAML());
            return node;
        }

        public Quaternion[] UnpackQuats()
        {
            var data = new Quaternion[m_NumItems];
            int indexPos = 0;
            int bitPos = 0;

            for (int i = 0; i < m_NumItems; i++)
            {
                uint flags = 0;

                int bits = 0;
                while (bits < 3)
                {
                    flags |= (uint)((m_Data[indexPos] >> bitPos) << bits);
                    int num = Math.Min(3 - bits, 8 - bitPos);
                    bitPos += num;
                    bits += num;
                    if (bitPos == 8)
                    {
                        indexPos++;
                        bitPos = 0;
                    }
                }
                flags &= 7;


                var q = new Quaternion();
                float sum = 0;
                for (int j = 0; j < 4; j++)
                {
                    if ((flags & 3) != j)
                    {
                        int bitSize = ((flags & 3) + 1) % 4 == j ? 9 : 10;
                        uint x = 0;

                        bits = 0;
                        while (bits < bitSize)
                        {
                            x |= (uint)((m_Data[indexPos] >> bitPos) << bits);
                            int num = Math.Min(bitSize - bits, 8 - bitPos);
                            bitPos += num;
                            bits += num;
                            if (bitPos == 8)
                            {
                                indexPos++;
                                bitPos = 0;
                            }
                        }
                        x &= (uint)((1 << bitSize) - 1);
                        q[j] = x / (0.5f * ((1 << bitSize) - 1)) - 1;
                        sum += q[j] * q[j];
                    }
                }

                int lastComponent = (int)(flags & 3);
                q[lastComponent] = (float)Math.Sqrt(1 - sum);
                if ((flags & 4) != 0u)
                    q[lastComponent] = -q[lastComponent];
                data[i] = q;
            }

            return data;
        }
    }

    public class CompressedAnimationCurve : IYAMLExportable
    {
        public string m_Path;
        public PackedIntVector m_Times;
        public PackedQuatVector m_Values;
        public PackedFloatVector m_Slopes;
        public int m_PreInfinity;
        public int m_PostInfinity;

        public CompressedAnimationCurve(ObjectReader reader)
        {
            m_Path = reader.ReadAlignedString();
            m_Times = new PackedIntVector(reader);
            m_Values = new PackedQuatVector(reader);
            m_Slopes = new PackedFloatVector(reader);
            m_PreInfinity = reader.ReadInt32();
            m_PostInfinity = reader.ReadInt32();
        }

        public YAMLNode ExportYAML(int[] version)
        {
            var node = new YAMLMappingNode();
            node.Add(nameof(m_Path), m_Path);
            node.Add(nameof(m_Times), m_Times.ExportYAML(version));
            node.Add(nameof(m_Values), m_Values.ExportYAML(version));
            node.Add(nameof(m_Slopes), m_Slopes.ExportYAML(version));
            node.Add(nameof(m_PreInfinity), m_PreInfinity);
            node.Add(nameof(m_PostInfinity), m_PostInfinity);
            return node;
        }
    }

    public class Vector3Curve : IYAMLExportable
    {
        public AnimationCurve<Vector3> curve;
        public string path;

        public Vector3Curve(string path)
        {
            curve = new AnimationCurve<Vector3>();
            this.path = path;
        }

        public Vector3Curve(ObjectReader reader)
        {
            curve = new AnimationCurve<Vector3>(reader, reader.ReadVector3);
            path = reader.ReadAlignedString();
        }

        public YAMLNode ExportYAML(int[] version)
        {
            YAMLMappingNode node = new YAMLMappingNode();
            node.Add(nameof(curve), curve.ExportYAML(version));
            node.Add(nameof(path), path);
            return node;
        }

        public override bool Equals(object obj)
        {
            if (obj is Vector3Curve vector3Curve)
            {
                return path == vector3Curve.path;
            }
            return false;
        }

        public override int GetHashCode()
        {
            int hash = 577;
            unchecked
            {
                hash = 419 + hash * path.GetHashCode();
            }
            return hash;
        }
    }

    public class FloatCurve : IYAMLExportable
    {
        public AnimationCurve<Float> curve;
        public string attribute;
        public string path;
        public ClassIDType classID;
        public PPtr<MonoScript> script;
        public int flags;

        public FloatCurve(string path, string attribute, ClassIDType classID, PPtr<MonoScript> script)
        {
            curve = new AnimationCurve<Float>();
            this.attribute = attribute;
            this.path = path;
            this.classID = classID;
            this.script = script;
            flags = 0;
        }

        public FloatCurve(ObjectReader reader)
        {
            var version = reader.version;

            curve = new AnimationCurve<Float>(reader, reader.ReadFloat);
            attribute = reader.ReadAlignedString();
            path = reader.ReadAlignedString();
            classID = (ClassIDType)reader.ReadInt32();
            script = new PPtr<MonoScript>(reader);
            if (version[0] == 2022 && version[1] >= 2) //2022.2 and up
            {
                flags = reader.ReadInt32();
            }
        }

        public YAMLNode ExportYAML(int[] version)
        {
            YAMLMappingNode node = new YAMLMappingNode();
            node.Add(nameof(curve), curve.ExportYAML(version));
            node.Add(nameof(attribute), attribute);
            node.Add(nameof(path), path);
            node.Add(nameof(classID), (int)classID);
            if (version[0] >= 2)
            {
                node.Add(nameof(script), script.ExportYAML(version));
            }
            node.Add(nameof(flags), flags);
            return node;
        }

        public override bool Equals(object obj)
        {
            if (obj is FloatCurve floatCurve)
            {
                return attribute == floatCurve.attribute && path == floatCurve.path && classID == floatCurve.classID;
            }
            return false;
        }

        public override int GetHashCode()
        {
            int hash = 17;
            unchecked
            {
                hash = hash * 23 + path.GetHashCode();
            }
            return hash;
        }
    }

    public class PPtrKeyframe : IYAMLExportable
    {
        public float time;
        public PPtr<Object> value;

        public PPtrKeyframe(float time, PPtr<Object> value)
        {
            this.time = time;
            this.value = value;
        }

        public PPtrKeyframe(ObjectReader reader)
        {
            time = reader.ReadSingle();
            value = new PPtr<Object>(reader);
        }
        public YAMLNode ExportYAML(int[] version)
        {
            var node = new YAMLMappingNode();
            node.Add(nameof(time), time);
            node.Add(nameof(value), value.ExportYAML(version));
            return node;
        }
    }

    public class PPtrCurve : IYAMLExportable
    {
        public List<PPtrKeyframe> curve;
        public string attribute;
        public string path;
        public int classID;
        public PPtr<MonoScript> script;
        public int flags;

        public PPtrCurve(string path, string attribute, ClassIDType classID, PPtr<MonoScript> script)
        {
            curve = new List<PPtrKeyframe>();
            this.attribute = attribute;
            this.path = path;
            this.classID = (int)classID;
            this.script = script;
            flags = 0;
        }

        public PPtrCurve(ObjectReader reader)
        {
            var version = reader.version;

            int numCurves = reader.ReadInt32();
            curve = new List<PPtrKeyframe>();
            for (int i = 0; i < numCurves; i++)
            {
                curve.Add(new PPtrKeyframe(reader));
            }

            attribute = reader.ReadAlignedString();
            path = reader.ReadAlignedString();
            classID = reader.ReadInt32();
            script = new PPtr<MonoScript>(reader);
            if (version[0] == 2022 && version[1] >= 2) //2022.2 and up
            {
                flags = reader.ReadInt32();
            }
        }

        public YAMLNode ExportYAML(int[] version)
        {
            YAMLMappingNode node = new YAMLMappingNode();
            node.Add(nameof(curve), curve.ExportYAML(version));
            node.Add(nameof(attribute), attribute);
            node.Add(nameof(path), path);
            node.Add(nameof(classID), (classID).ToString());
            node.Add(nameof(script), script.ExportYAML(version));
            node.Add(nameof(flags), flags);
            return node;
        }

        public override bool Equals(object obj)
        {
            if (obj is PPtrCurve pptrCurve)
            {
                return this == pptrCurve;
            }
            return false;
        }

        public override int GetHashCode()
        {
            int hash = 113;
            unchecked
            {
                hash = hash + 457 * attribute.GetHashCode();
                hash = hash * 433 + path.GetHashCode();
                hash = hash * 223 + classID.GetHashCode();
                hash = hash * 911 + script.GetHashCode();
                hash = hash * 342 + flags.GetHashCode();
            }
            return hash;
        }
    }

    public class AABB : IYAMLExportable
    {
        public Vector3 m_Center;
        public Vector3 m_Extent;

        public AABB(ObjectReader reader)
        {
            m_Center = reader.ReadVector3();
            m_Extent = reader.ReadVector3();
        }

        public YAMLNode ExportYAML(int[] version)
        {
            var node = new YAMLMappingNode();
            node.Add(nameof(m_Center), m_Center.ExportYAML(version));
            node.Add(nameof(m_Extent), m_Extent.ExportYAML(version));
            return node;
        }
        public AABB(Vector3 center, Vector3 extent)
        {
            m_Center = center;
            m_Extent = extent;
        }
    }

    public class HandPose
    {
        public XForm m_GrabX;
        public float[] m_DoFArray;
        public float m_Override;
        public float m_CloseOpen;
        public float m_InOut;
        public float m_Grab;
        public HandPose() { }

        public HandPose(ObjectReader reader)
        {
            m_GrabX = reader.ReadXForm();
            m_DoFArray = reader.ReadSingleArray();
            m_Override = reader.ReadSingle();
            m_CloseOpen = reader.ReadSingle();
            m_InOut = reader.ReadSingle();
            m_Grab = reader.ReadSingle();
        }

        public static HandPose ParseGI(ObjectReader reader)
        {
            var handPose = new HandPose();

            handPose.m_GrabX = reader.ReadXForm4();
            handPose.m_DoFArray = reader.ReadSingleArray(20);
            handPose.m_Override = reader.ReadSingle();
            handPose.m_CloseOpen = reader.ReadSingle();
            handPose.m_InOut = reader.ReadSingle();
            handPose.m_Grab = reader.ReadSingle();

            return handPose;
        }
    }

    public class HumanGoal
    {
        public XForm m_X;
        public float m_WeightT;
        public float m_WeightR;
        public Vector3 m_HintT;
        public float m_HintWeightT;
        public HumanGoal() { }

        public HumanGoal(ObjectReader reader)
        {
            var version = reader.version;
            m_X = reader.ReadXForm();
            m_WeightT = reader.ReadSingle();
            m_WeightR = reader.ReadSingle();
            if (version[0] >= 5)//5.0 and up
            {
                m_HintT = version[0] > 5 || (version[0] == 5 && version[1] >= 4) ? reader.ReadVector3() : (Vector3)reader.ReadVector4();//5.4 and up
                m_HintWeightT = reader.ReadSingle();
            }
        }

        public static HumanGoal ParseGI(ObjectReader reader)
        {
            var humanGoal = new HumanGoal();

            humanGoal.m_X = reader.ReadXForm4();
            humanGoal.m_WeightT = reader.ReadSingle();
            humanGoal.m_WeightR = reader.ReadSingle();

            humanGoal.m_HintT = (Vector3)reader.ReadVector4();
            humanGoal.m_HintWeightT = reader.ReadSingle();

            var m_HintR = (Vector3)reader.ReadVector4();
            var m_HintWeightR = reader.ReadSingle();

            return humanGoal;
        }
    }

    public class HumanPose
    {
        public XForm m_RootX;
        public Vector3 m_LookAtPosition;
        public Vector4 m_LookAtWeight;
        public List<HumanGoal> m_GoalArray;
        public HandPose m_LeftHandPose;
        public HandPose m_RightHandPose;
        public float[] m_DoFArray;
        public Vector3[] m_TDoFArray;
        public HumanPose() { }

        public HumanPose(ObjectReader reader)
        {
            var version = reader.version;
            m_RootX = reader.ReadXForm();
            m_LookAtPosition = version[0] > 5 || (version[0] == 5 && version[1] >= 4) ? reader.ReadVector3() : (Vector3)reader.ReadVector4();//5.4 and up
            m_LookAtWeight = reader.ReadVector4();

            int numGoals = reader.ReadInt32();
            m_GoalArray = new List<HumanGoal>();
            for (int i = 0; i < numGoals; i++)
            {
                m_GoalArray.Add(new HumanGoal(reader));
            }

            m_LeftHandPose = new HandPose(reader);
            m_RightHandPose = new HandPose(reader);

            m_DoFArray = reader.ReadSingleArray();

            if (version[0] > 5 || (version[0] == 5 && version[1] >= 2))//5.2 and up
            {
                m_TDoFArray = reader.ReadVector3Array();
            }
        }

        public static HumanPose ParseGI(ObjectReader reader)
        {
            var version = reader.version;
            var humanPose = new HumanPose();

            humanPose.m_RootX = reader.ReadXForm4();
            humanPose.m_LookAtPosition = (Vector3)reader.ReadVector4();
            humanPose.m_LookAtWeight = reader.ReadVector4();

            humanPose.m_GoalArray = new List<HumanGoal>();
            for (int i = 0; i < 4; i++)
            {
                humanPose.m_GoalArray.Add(HumanGoal.ParseGI(reader));
            }

            humanPose.m_LeftHandPose = HandPose.ParseGI(reader);
            humanPose.m_RightHandPose = HandPose.ParseGI(reader);

            humanPose.m_DoFArray = reader.ReadSingleArray(0x37);

            humanPose.m_TDoFArray = reader.ReadVector4Array(0x15).Select(x => (Vector3)x).ToArray();

            reader.Position += 4;

            return humanPose;
        }
    }

    public abstract class ACLClip
    {
        public virtual bool IsSet => false;
        public virtual uint CurveCount => 0;
        public abstract void Read(ObjectReader reader);
    }

    public class EmptyACLClip : ACLClip
    {
        public override void Read(ObjectReader reader) { }
    }

    public class MHYACLClip : ACLClip
    {
        public uint m_CurveCount;
        public uint m_ConstCurveCount;

        public byte[] m_ClipData;

        public override bool IsSet => !m_ClipData.IsNullOrEmpty();
        public override uint CurveCount => m_CurveCount;

        public MHYACLClip()
        {
            m_CurveCount = 0;
            m_ConstCurveCount = 0;
            m_ClipData = Array.Empty<byte>();
        }
        public override void Read(ObjectReader reader)
        {
            var byteCount = reader.ReadInt32();

            if (reader.Game.Type.IsSRGroup())
            {
                byteCount *= 4;
            }

            m_ClipData = reader.ReadBytes(byteCount);
            reader.AlignStream();

            m_CurveCount = reader.ReadUInt32();

            if (reader.Game.Type.IsSRGroup())
            {
                m_ConstCurveCount = reader.ReadUInt32();
            }
        }
    }

    public class AclTransformTrackIDToBindingCurveID
    {
        public uint rotationIDToBindingCurveID;
        public uint positionIDToBindingCurveID;
        public uint scaleIDToBindingCurveID;
        public AclTransformTrackIDToBindingCurveID(ObjectReader reader)
        {
            rotationIDToBindingCurveID = reader.ReadUInt32();
            positionIDToBindingCurveID = reader.ReadUInt32();
            scaleIDToBindingCurveID = reader.ReadUInt32();
        }
    }

    public class LnDACLClip : ACLClip
    {
        public uint m_CurveCount;
        public byte[] m_ClipData;

        public override bool IsSet => !m_ClipData.IsNullOrEmpty();
        public override uint CurveCount => m_CurveCount;
        public override void Read(ObjectReader reader)
        {
            m_CurveCount = reader.ReadUInt32();
            var compressedTransformTracksSize = reader.ReadUInt32();
            var compressedScalarTracksSize = reader.ReadUInt32();
            var aclTransformCount = reader.ReadUInt32();
            var aclScalarCount = reader.ReadUInt32();

            var compressedTransformTracksCount = reader.ReadInt32() * 0x10;
            var compressedTransformTracks = reader.ReadBytes(compressedTransformTracksCount);
            var compressedScalarTracksCount = reader.ReadInt32() * 0x10;
            var compressedScalarTracks = reader.ReadBytes(compressedScalarTracksCount);

            int numaclTransformTrackIDToBindingCurveID = reader.ReadInt32();
            var aclTransformTrackIDToBindingCurveID = new List<AclTransformTrackIDToBindingCurveID>();
            for (int i = 0; i < numaclTransformTrackIDToBindingCurveID; i++)
            {
                aclTransformTrackIDToBindingCurveID.Add(new AclTransformTrackIDToBindingCurveID(reader));
            }
            var aclScalarTrackIDToBindingCurveID = reader.ReadUInt32Array();
        }
    }

    public class GIACLClip : ACLClip
    {
        public uint m_CurveCount;
        public uint m_ConstCurveCount;

        public byte[] m_ClipData;
        public byte[] m_DatabaseData;

        public override bool IsSet => !m_ClipData.IsNullOrEmpty() && !m_DatabaseData.IsNullOrEmpty();
        public override uint CurveCount => m_CurveCount;

        public GIACLClip()
        {
            m_CurveCount = 0;
            m_ConstCurveCount = 0;
            m_ClipData = Array.Empty<byte>();
            m_DatabaseData = Array.Empty<byte>();
        }

        public override void Read(ObjectReader reader)
        {
            var aclTracksCount = (int)reader.ReadUInt64();
            var aclTracksOffset = reader.Position + reader.ReadInt64();
            var aclTracksCurveCount = reader.ReadUInt32();
            if (aclTracksOffset > reader.Length)
            {
                throw new IOException("Offset outside of range");
            }

            var pos = reader.Position;
            reader.Position = aclTracksOffset;

            var tracksBytes = reader.ReadBytes(aclTracksCount);
            reader.AlignStream();

            using var tracksMS = new MemoryStream();
            tracksMS.Write(tracksBytes);
            tracksMS.AlignStream();
            m_CurveCount = aclTracksCurveCount;
            m_ClipData = tracksMS.ToArray();

            reader.Position = pos;

            var aclDatabaseCount = reader.ReadInt32();
            var aclDatabaseOffset = reader.Position + reader.ReadInt64();
            var aclDatabaseCurveCount = (uint)reader.ReadUInt64();
            if (aclDatabaseOffset > reader.Length)
            {
                throw new IOException("Offset outside of range");
            }

            pos = reader.Position;
            reader.Position = aclDatabaseOffset;

            var databaseBytes = reader.ReadBytes(aclDatabaseCount);
            reader.AlignStream();

            using var databaseMS = new MemoryStream();
            databaseMS.Write(databaseBytes);
            databaseMS.AlignStream();

            m_ConstCurveCount = aclDatabaseCurveCount;
            m_DatabaseData = databaseMS.ToArray();

            reader.Position = pos;
        }
    }

    public class StreamedClip
    {
        public uint[] data;
        public uint curveCount;
        public StreamedClip() { }

        public StreamedClip(ObjectReader reader)
        {
            data = reader.ReadUInt32Array();
            curveCount = reader.ReadUInt32();
        }
        public static StreamedClip ParseGI(ObjectReader reader)
        {
            var streamedClipCount = (int)reader.ReadUInt64();
            var streamedClipOffset = reader.Position + reader.ReadInt64();
            var streamedClipCurveCount = (uint)reader.ReadUInt64();
            if (streamedClipOffset > reader.Length)
            {
                throw new IOException("Offset outside of range");
            }

            var pos = reader.Position;
            reader.Position = streamedClipOffset;

            var streamedClip = new StreamedClip()
            {
                data = reader.ReadUInt32Array(streamedClipCount),
                curveCount = streamedClipCurveCount
            };

            reader.Position = pos;

            return streamedClip;
        }

        public class StreamedCurveKey
        {
            public int index;
            public float[] coeff;

            public float value;
            public float outSlope;
            public float inSlope;

            public StreamedCurveKey(EndianBinaryReader reader)
            {
                index = reader.ReadInt32();
                coeff = reader.ReadSingleArray(4);

                outSlope = coeff[2];
                value = coeff[3];
            }

            public float CalculateNextInSlope(float dx, StreamedCurveKey rhs)
            {
                //Stepped
                if (coeff[0] == 0f && coeff[1] == 0f && coeff[2] == 0f)
                {
                    return float.PositiveInfinity;
                }

                dx = Math.Max(dx, 0.0001f);
                var dy = rhs.value - value;
                var length = 1.0f / (dx * dx);
                var d1 = outSlope * dx;
                var d2 = dy + dy + dy - d1 - d1 - coeff[1] / length;
                return d2 / dx;
            }
        }

        public class StreamedFrame
        {
            public float time;
            public List<StreamedCurveKey> keyList;

            public StreamedFrame(EndianBinaryReader reader)
            {
                time = reader.ReadSingle();

                int numKeys = reader.ReadInt32();
                keyList = new List<StreamedCurveKey>();
                for (int i = 0; i < numKeys; i++)
                {
                    keyList.Add(new StreamedCurveKey(reader));
                }
            }
        }

        public List<StreamedFrame> ReadData()
        {
            var frameList = new List<StreamedFrame>();
            var buffer = new byte[data.Length * 4];
            Buffer.BlockCopy(data, 0, buffer, 0, buffer.Length);
            using (var reader = new EndianBinaryReader(new MemoryStream(buffer), EndianType.LittleEndian))
            {
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    frameList.Add(new StreamedFrame(reader));
                }
            }

            for (int frameIndex = 2; frameIndex < frameList.Count - 1; frameIndex++)
            {
                var frame = frameList[frameIndex];
                foreach (var curveKey in frame.keyList)
                {
                    for (int i = frameIndex - 1; i >= 0; i--)
                    {
                        var preFrame = frameList[i];
                        var preCurveKey = preFrame.keyList.FirstOrDefault(x => x.index == curveKey.index);
                        if (preCurveKey != null)
                        {
                            curveKey.inSlope = preCurveKey.CalculateNextInSlope(frame.time - preFrame.time, curveKey);
                            break;
                        }
                    }
                }
            }
            return frameList;
        }
    }

    public class DenseClip
    {
        public int m_FrameCount;
        public uint m_CurveCount;
        public float m_SampleRate;
        public float m_BeginTime;
        public float[] m_SampleArray;
        public ushort[] m_HokCompressedSamples;
        public List<HokDenseRange> m_HokRanges;
        public DenseClip() { }

        public DenseClip(ObjectReader reader)
        {
            m_FrameCount = reader.ReadInt32();
            m_CurveCount = reader.ReadUInt32();
            m_SampleRate = reader.ReadSingle();
            m_BeginTime = reader.ReadSingle();
            m_SampleArray = reader.ReadSingleArray();
            if (HokAnimationLayout.Applies(reader))
            {
                m_HokCompressedSamples = reader.ReadUInt16Array();
                reader.AlignStream();
                int count = reader.ReadInt32();
                if (count < 0 || count > reader.byteSize / 12)
                {
                    throw new InvalidDataException("Invalid HOK range count.");
                }
                m_HokRanges = new List<HokDenseRange>();
                for (int i = 0; i < count; i++)
                {
                    m_HokRanges.Add(new HokDenseRange(reader));
                }
                if (m_HokCompressedSamples.Length > 0)
                {
                    if (m_SampleArray.Length != 0)
                    {
                        throw new InvalidDataException("Both HOK dense encodings are populated.");
                    }
                    m_SampleArray = HokLegacyAnimation.DecodeHokSamples(m_FrameCount, m_CurveCount, m_HokCompressedSamples, m_HokRanges);
                }
            }
        }
        public static DenseClip ParseGI(ObjectReader reader)
        {
            var denseClip = new DenseClip();

            denseClip.m_FrameCount = reader.ReadInt32();
            denseClip.m_CurveCount = reader.ReadUInt32();
            denseClip.m_SampleRate = reader.ReadSingle();
            denseClip.m_BeginTime = reader.ReadSingle();

            var denseClipCount = (int)reader.ReadUInt64();
            var denseClipOffset = reader.Position + reader.ReadInt64();
            if (denseClipOffset > reader.Length)
            {
                throw new IOException("Offset outside of range");
            }

            var pos = reader.Position;
            reader.Position = denseClipOffset;

            denseClip.m_SampleArray = reader.ReadSingleArray(denseClipCount);

            reader.Position = pos;

            return denseClip;
        }
    }
    public class ACLDenseClip : DenseClip
    {
        public int m_ACLType;
        public byte[] m_ACLArray;
        public float m_PositionFactor;
        public float m_EulerFactor;
        public float m_ScaleFactor;
        public float m_FloatFactor;
        public uint m_nPositionCurves;
        public uint m_nRotationCurves;
        public uint m_nEulerCurves;
        public uint m_nScaleCurves;
        public uint m_nGenericCurves;

        public ACLDenseClip(ObjectReader reader) : base(reader)
        {
            m_ACLType = reader.ReadInt32();
            if (reader.Game.Type.IsArknightsEndfield())
            {
                m_ACLArray = reader.ReadUInt8Array();
                reader.AlignStream();
                m_PositionFactor = reader.ReadSingle();
                m_EulerFactor = reader.ReadSingle();
                m_ScaleFactor = reader.ReadSingle();
                m_FloatFactor = reader.ReadSingle();
                m_nPositionCurves = reader.ReadUInt32();
                m_nRotationCurves = reader.ReadUInt32();
                m_nEulerCurves = reader.ReadUInt32();
                m_nScaleCurves = reader.ReadUInt32();
            }
            else if (reader.Game.Type.IsExAstris())
            {
                m_nPositionCurves = reader.ReadUInt32();
                m_nRotationCurves = reader.ReadUInt32();
                m_nEulerCurves = reader.ReadUInt32();
                m_nScaleCurves = reader.ReadUInt32();
                m_nGenericCurves = reader.ReadUInt32();
                m_PositionFactor = reader.ReadSingle();
                m_EulerFactor = reader.ReadSingle();
                m_ScaleFactor = reader.ReadSingle();
                m_FloatFactor = reader.ReadSingle();
                m_ACLArray = reader.ReadUInt8Array();
                reader.AlignStream();
            }
            Process();
        }

        private void Process()
        {
            if (m_ACLType == 0 || !m_SampleArray.IsNullOrEmpty())
            {
                return;
            }

            var sampleArray = new List<float>();

            var size = m_ACLType >> 2;
            var factor = (float)((1 << m_ACLType) - 1);
            var aclSpan = m_ACLArray.ToUInt4Array().AsSpan();
            var buffer = (stackalloc byte[8]);

            for (int i = 0; i < m_FrameCount; i++)
            {
                var index = i * (int)(m_CurveCount * size);
                for (int j = 0; j < m_nPositionCurves; j++)
                {
                    sampleArray.Add(ReadCurve(aclSpan, m_PositionFactor, ref index));
                }
                for (int j = 0; j < m_nRotationCurves; j++)
                {
                    sampleArray.Add(ReadCurve(aclSpan, 1.0f, ref index));
                }
                for (int j = 0; j < m_nEulerCurves; j++)
                {
                    sampleArray.Add(ReadCurve(aclSpan, m_EulerFactor, ref index));
                }
                for (int j = 0; j < m_nScaleCurves; j++)
                {
                    sampleArray.Add(ReadCurve(aclSpan, m_ScaleFactor, ref index));
                }
                var m_nFloatCurves = m_CurveCount - (m_nPositionCurves + m_nRotationCurves + m_nEulerCurves + m_nScaleCurves + m_nGenericCurves);
                for (int j = 0; j < m_nFloatCurves; j++)
                {
                    sampleArray.Add(ReadCurve(aclSpan, m_FloatFactor, ref index));
                }
            }

            m_SampleArray = sampleArray.ToArray();
        }

        private float ReadCurve(Span<byte> aclSpan, float curveFactor, ref int curveIndex)
        {
            var buffer = (stackalloc byte[8]);

            var curveSize = m_ACLType >> 2;
            var factor = (float)((1 << m_ACLType) - 1);

            aclSpan.Slice(curveIndex, curveSize).CopyTo(buffer);
            var temp = buffer.ToArray().ToUInt8Array(0, curveSize);
            buffer.Clear();
            temp.CopyTo(buffer);

            float curve;
            var value = BitConverter.ToUInt64(buffer);
            if (value != 0)
            {
                curve = ((value / factor) - 0.5f) * 2;
            }
            else
            {
                curve = -1.0f;
            }

            curve *= curveFactor;
            curveIndex += curveSize;

            return curve;
        }
    }

    public class ConstantClip
    {
        public float[] data;
        public ConstantClip() { }

        public ConstantClip(ObjectReader reader)
        {
            data = reader.ReadSingleArray();
        }
        public static ConstantClip ParseGI(ObjectReader reader)
        {
            var constantClipCount = (int)reader.ReadUInt64();
            var constantClipOffset = reader.Position + reader.ReadInt64();
            if (constantClipOffset > reader.Length)
            {
                throw new IOException("Offset outside of range");
            }

            var pos = reader.Position;
            reader.Position = constantClipOffset;

            var constantClip = new ConstantClip();
            constantClip.data = reader.ReadSingleArray(constantClipCount);

            reader.Position = pos;

            return constantClip;
        }
    }

    /// <summary>
    /// Honor of Kings (王者荣耀) modified engine layout gate.
    /// The 2022.3.5 engine revision stores extra data in AnimationClip, Clip and
    /// DenseClip. Other revisions are not known to use the same layout.
    /// </summary>
    public static class HokAnimationLayout
    {
        public static bool Applies(ObjectReader reader)
        {
            var version = reader.version;
            return reader.Game.Type.IsHonorOfKings()
                && version.Length >= 2
                && version[0] == 2022 && version[1] == 3;
        }
    }

    /// <summary>
    /// One HOK bit-packed dense group descriptor (12 bytes on disk).
    ///   low  8 bits : group type (1 = quaternion, 2 = vector3, 3 = scalar)
    ///   high 24 bits: bit width of each stored component (0 = full UInt16)
    /// </summary>
    public class HokDenseRange
    {
        public float minimum;
        public float step;
        public uint kind;

        public int GroupType => (int)(kind & 255);
        public int BitWidth => (int)(kind >> 8);
        public int Dimension => GroupType == 1 ? 4 : GroupType == 2 ? 3 : GroupType == 3 ? 1 : throw new InvalidDataException($"Unknown HOK dense group kind: {kind}.");

        public HokDenseRange() { }

        public HokDenseRange(ObjectReader reader)
        {
            minimum = reader.ReadSingle();
            step = reader.ReadSingle();
            kind = reader.ReadUInt32();
        }
    }

    /// <summary>
    /// One entry of the HOK legacy (non humanoid) animation binding table.
    /// The path is resolved from the outer name / path-index tables.
    /// </summary>
    public class HokLegacyBinding
    {
        public string attribute;
        public PPtr<MonoScript> script;
        public int classID;
        public uint hash;
        public string path;

        public HokLegacyBinding(ObjectReader reader)
        {
            attribute = reader.ReadAlignedString();
            script = new PPtr<MonoScript>(reader);
            classID = reader.ReadInt32();
            hash = reader.ReadUInt32();
        }
    }

    /// <summary>
    /// HOK legacy animation block, stored directly after m_Bounds.
    /// It contains a full standalone <see cref="Clip"/> plus the path/binding table
    /// that maps its curves onto scene objects.
    /// </summary>
    public class HokLegacyAnimation
    {
        public uint sizeField;
        public Clip clip;
        public uint? unknownWord;
        public float startTime;
        public float stopTime;
        public string[] names;
        public int[] pathIndices;
        public List<HokLegacyBinding> bindings;

        public HokLegacyAnimation(ObjectReader reader)
        {
            sizeField = reader.ReadUInt32();
            clip = new Clip(reader);

            // The older UInt16 layout has an additional word. Bit packed dense
            // groups are followed directly by the clip time range.
            var tail = reader.Position;
            bool legacyWord = sizeField != 0
                && clip.m_DenseClip.m_HokCompressedSamples.Length > 0
                && !clip.m_DenseClip.m_HokRanges.Any(r => r.BitWidth != 0);
            try
            {
                ReadTail(reader, legacyWord);
            }
            catch (Exception first) when (first is InvalidDataException || first is EndOfStreamException || first is OverflowException)
            {
                // Streamed/constant only clips exist in both revisions, so an empty
                // dense table cannot identify the tail layout. Validate the complete
                // name/path/binding tables at the alternate offset.
                reader.Position = tail;
                try
                {
                    ReadTail(reader, !legacyWord);
                }
                catch (Exception second) when (second is InvalidDataException || second is EndOfStreamException || second is OverflowException)
                {
                    throw new InvalidDataException($"Invalid HOK legacy tail in both layouts: {first.Message}; {second.Message}", second);
                }
            }
        }

        private void ReadTail(ObjectReader reader, bool extraWord)
        {
            unknownWord = extraWord ? reader.ReadUInt32() : (uint?)null;
            startTime = reader.ReadSingle();
            stopTime = reader.ReadSingle();
            if (!float.IsFinite(startTime) || !float.IsFinite(stopTime) || stopTime < startTime)
            {
                throw new InvalidDataException("Invalid HOK legacy time interval.");
            }
            names = reader.ReadStringArray();
            pathIndices = reader.ReadInt32Array();
            int count = reader.ReadInt32();
            if (count < 0 || count > reader.byteSize / 24)
            {
                throw new InvalidDataException("Invalid HOK legacy binding count.");
            }

            bindings = new List<HokLegacyBinding>();
            var paths = new List<string>();
            var path = new List<string>();
            foreach (int index in pathIndices)
            {
                if (index == -1)
                {
                    paths.Add(string.Join("/", path));
                    path.Clear();
                }
                else
                {
                    if (index < 0 || index >= names.Length)
                    {
                        throw new InvalidDataException("Invalid HOK legacy path index.");
                    }
                    path.Add(names[index]);
                }
            }
            if (path.Count != 0 || paths.Count != count)
            {
                throw new InvalidDataException("HOK legacy paths/bindings mismatch.");
            }

            for (int i = 0; i < count; i++)
            {
                var binding = new HokLegacyBinding(reader) { path = paths[i] };
                bindings.Add(binding);
            }
        }

        /// <summary>
        /// Dimension of one binding inside the HOK curve index space.
        /// Transform bindings use the Unity binding codes, every other binding is scalar.
        /// </summary>
        public static int GetBindingDimension(HokLegacyBinding binding)
        {
            if (binding.classID == -1)
            {
                switch (binding.attribute)
                {
                    case "1": return 3; // kBindTransformPosition
                    case "2": return 4; // kBindTransformRotation
                    case "3": return 3; // kBindTransformScale
                    case "4": return 3; // kBindTransformEuler
                }
            }
            return 1;
        }

        private static readonly Quaternion HokQuaternionWeight = new Quaternion(1.0f / 3.0f, 1.0f / 3.0f, 1.0f / 3.0f, 1.0f / 3.0f);

        private static bool IsRealTime(float time) => float.IsFinite(time) && Math.Abs(time) < float.MaxValue;

        private static readonly Vector3 HokVector3Weight = new Vector3(1.0f / 3.0f, 1.0f / 3.0f, 1.0f / 3.0f);
        private static readonly Float HokFloatWeight = new Float(1.0f / 3.0f);

        /// <summary>
        /// Rebuilds the standard curve lists from the embedded clip. The stock exporters
        /// only walk m_RotationCurves / m_PositionCurves / m_ScaleCurves / m_EulerCurves /
        /// m_FloatCurves, which are empty on disk for HOK legacy clips.
        ///
        /// Curve index space (verified against every HOK legacy sample):
        ///   streamed [0, streamedCurveCount)
        ///   dense    [streamedCurveCount, + denseCurveCount)
        ///   constant [streamedCurveCount + denseCurveCount, total)
        /// where total equals the sum of all binding dimensions.
        /// </summary>
        public void BuildCurves(List<QuaternionCurve> rotations, List<Vector3Curve> positions,
            List<Vector3Curve> scales, List<Vector3Curve> eulers, List<FloatCurve> floats)
        {
            if (clip == null || bindings == null || bindings.Count == 0)
            {
                return;
            }

            var dims = new int[bindings.Count];
            int total = 0;
            for (int i = 0; i < bindings.Count; i++)
            {
                dims[i] = GetBindingDimension(bindings[i]);
                total += dims[i];
            }

            int streamedCount = (int)clip.m_StreamedClip.curveCount;
            int denseCount = (int)clip.m_DenseClip.m_CurveCount;
            int constantCount = clip.m_ConstantClip?.data?.Length ?? 0;
            if (total != streamedCount + denseCount + constantCount)
            {
                // unknown revision of the block, never guess
                return;
            }

            var bindingOfComponent = new int[total];
            var componentInBinding = new int[total];
            for (int i = 0, c = 0; i < bindings.Count; i++)
            {
                for (int k = 0; k < dims[i]; k++, c++)
                {
                    bindingOfComponent[c] = i;
                    componentInBinding[c] = k;
                }
            }

            var times = new List<float>[bindings.Count];
            var rows = new List<float[]>[bindings.Count];

            void AddSample(int component, float time, float value)
            {
                if (component < 0 || component >= total || !IsRealTime(time))
                {
                    return;
                }
                int b = bindingOfComponent[component];
                if (times[b] == null)
                {
                    times[b] = new List<float>();
                    rows[b] = new List<float[]>();
                }
                float[] row;
                if (times[b].Count > 0 && times[b][times[b].Count - 1] == time)
                {
                    row = rows[b][rows[b].Count - 1];
                }
                else
                {
                    row = new float[dims[b]];
                    if (rows[b].Count > 0)
                    {
                        Array.Copy(rows[b][rows[b].Count - 1], row, dims[b]);
                    }
                    times[b].Add(time);
                    rows[b].Add(row);
                }
                row[componentInBinding[component]] = value;
            }

            // 1) streamed keyframes. Unity brackets the range with -FLT_MAX / +FLT_MAX
            //    sentinel frames, AddSample drops them.
            var frames = clip.m_StreamedClip.ReadData();
            foreach (var frame in frames)
            {
                foreach (var key in frame.keyList)
                {
                    AddSample(key.index, frame.time, key.value);
                }
            }

            // 2) dense per frame samples
            var dense = clip.m_DenseClip;
            if (denseCount > 0 && dense.m_FrameCount > 0 && dense.m_SampleRate > 0 && dense.m_SampleArray != null)
            {
                for (int frame = 0; frame < dense.m_FrameCount; frame++)
                {
                    int frameOffset = frame * denseCount;
                    if (frameOffset + denseCount > dense.m_SampleArray.Length)
                    {
                        break;
                    }
                    float time = dense.m_BeginTime + frame / dense.m_SampleRate;
                    for (int c = 0; c < denseCount; c++)
                    {
                        AddSample(streamedCount + c, time, dense.m_SampleArray[frameOffset + c]);
                    }
                }
            }

            // 3) constant curves, pinned to the block time range
            var constant = clip.m_ConstantClip;
            if (constantCount > 0 && constant?.data != null)
            {
                for (int c = 0; c < constantCount; c++)
                {
                    int component = streamedCount + denseCount + c;
                    AddSample(component, startTime, constant.data[c]);
                    AddSample(component, stopTime, constant.data[c]);
                }
            }

            // 4) group the rows into curves
            for (int i = 0; i < bindings.Count; i++)
            {
                if (times[i] == null || times[i].Count == 0)
                {
                    continue;
                }
                var binding = bindings[i];
                var path = binding.path ?? string.Empty;
                if (binding.classID == -1)
                {
                    switch (binding.attribute)
                    {
                        case "2" when dims[i] == 4:
                            rotations.Add(BuildQuaternionCurve(path, times[i], rows[i]));
                            continue;
                        case "1" when dims[i] == 3:
                            positions.Add(BuildVector3Curve(path, times[i], rows[i]));
                            continue;
                        case "3" when dims[i] == 3:
                            scales.Add(BuildVector3Curve(path, times[i], rows[i]));
                            continue;
                        case "4" when dims[i] == 3:
                            eulers.Add(BuildVector3Curve(path, times[i], rows[i]));
                            continue;
                    }
                }
                floats.Add(BuildFloatCurve(binding, path, times[i], rows[i]));
            }
        }

        private static QuaternionCurve BuildQuaternionCurve(string path, List<float> times, List<float[]> rows)
        {
            var curve = new QuaternionCurve(path);
            for (int k = 0; k < times.Count; k++)
            {
                var r = rows[k];
                curve.curve.m_Curve.Add(new Keyframe<Quaternion>(times[k],
                    new Quaternion(r[0], r[1], r[2], r[3]),
                    new Quaternion(0, 0, 0, 0), new Quaternion(0, 0, 0, 0), HokQuaternionWeight));
            }
            return curve;
        }

        private static Vector3Curve BuildVector3Curve(string path, List<float> times, List<float[]> rows)
        {
            var curve = new Vector3Curve(path);
            for (int k = 0; k < times.Count; k++)
            {
                var r = rows[k];
                curve.curve.m_Curve.Add(new Keyframe<Vector3>(times[k],
                    new Vector3(r[0], r[1], r[2]),
                    new Vector3(0, 0, 0), new Vector3(0, 0, 0), HokVector3Weight));
            }
            return curve;
        }

        private static FloatCurve BuildFloatCurve(HokLegacyBinding binding, string path, List<float> times, List<float[]> rows)
        {
            var classID = binding.classID == -1 ? ClassIDType.Transform : (ClassIDType)binding.classID;
            var curve = new FloatCurve(path, binding.attribute, classID, binding.script);
            for (int k = 0; k < times.Count; k++)
            {
                curve.curve.m_Curve.Add(new Keyframe<Float>(times[k], new Float(rows[k][0]),
                    new Float(0), new Float(0), HokFloatWeight));
            }
            return curve;
        }

        /// <summary>
        /// Expands the HOK bit packed dense encoding into a flat
        /// [frame][curve] float sample array.
        /// </summary>
        public static float[] DecodeHokSamples(int frames, uint curves, ushort[] words, IList<HokDenseRange> ranges)
        {
            if (frames < 0 || ranges.Sum(r => (long)r.Dimension) != curves)
            {
                throw new InvalidDataException("HOK dense curve dimensions mismatch.");
            }

            long bitsPerFrame = 0;
            foreach (var r in ranges)
            {
                if (r.BitWidth > 16 || !float.IsFinite(r.minimum) || !float.IsFinite(r.step) || r.step < 0)
                {
                    throw new InvalidDataException($"Invalid HOK dense range: kind={r.kind}.");
                }
                // No precision byte: the older layout stores every component as a
                // full UInt16. Newer groups encode precision in the high byte;
                // quaternions store a three bit selector/sign and three components.
                bitsPerFrame += r.BitWidth == 0
                    ? r.Dimension * 16
                    : r.GroupType == 1 ? 3 + r.BitWidth * 3 : r.Dimension * r.BitWidth;
            }

            long wordsPerFrame = (bitsPerFrame + 15) / 16;

            // HOK's bit writer can retain its empty next word when the frame ends
            // exactly on a word boundary. This is per frame padding, not another
            // curve. Accept it only when every retained word is zero.
            if (bitsPerFrame > 0 && bitsPerFrame % 16 == 0 && words.LongLength == frames * (wordsPerFrame + 1))
            {
                for (int f = 0; f < frames; f++)
                {
                    if (words[(f + 1) * (wordsPerFrame + 1) - 1] != 0)
                    {
                        throw new InvalidDataException("Nonzero HOK dense frame padding.");
                    }
                }
                wordsPerFrame++;
            }

            long count = (long)frames * curves;
            if (wordsPerFrame * frames != words.LongLength || count > 128 * 1024 * 1024)
            {
                throw new InvalidDataException($"HOK dense packed frame length mismatch: frames={frames}, curves={curves}, words={words.Length}, bitsPerFrame={bitsPerFrame}.");
            }

            var result = new float[checked((int)count)];
            int output = 0;
            for (int frame = 0; frame < frames; frame++)
            {
                // Every frame starts on a UInt16 boundary, not on the next bit.
                long bit = frame * wordsPerFrame * 16;

                uint ReadBits(int width)
                {
                    uint value = 0;
                    for (int shift = 0; shift < width; shift++, bit++)
                    {
                        value |= (uint)((words[bit / 16] >> (int)(bit % 16)) & 1) << shift;
                    }
                    return value;
                }

                foreach (var r in ranges)
                {
                    if (r.GroupType == 1 && r.BitWidth != 0)
                    {
                        // HOK writes its selector/sign AFTER the three values.
                        float x = r.minimum + ReadBits(r.BitWidth) * r.step;
                        float y = r.minimum + ReadBits(r.BitWidth) * r.step;
                        float z = r.minimum + ReadBits(r.BitWidth) * r.step;
                        uint flags = ReadBits(3);
                        int omitted = (int)(flags & 3);
                        double sum = 0;
                        int stored = 0;
                        for (int c = 0; c < 4; c++)
                        {
                            if (c == omitted)
                            {
                                continue;
                            }
                            float value = stored == 0 ? x : stored == 1 ? y : z;
                            stored++;
                            result[output + c] = value;
                            sum += (double)value * value;
                        }
                        if (sum > 1.01 || !double.IsFinite(sum))
                        {
                            throw new InvalidDataException("Invalid HOK packed quaternion components.");
                        }
                        // HOK selector bit 2 SET means POSITIVE.
                        result[output + omitted] = (float)Math.Sqrt(Math.Max(0, 1 - sum)) * ((flags & 4) != 0 ? 1 : -1);
                        output += 4;
                    }
                    else
                    {
                        for (int c = 0; c < r.Dimension; c++)
                        {
                            result[output++] = r.minimum + ReadBits(r.BitWidth == 0 ? 16 : r.BitWidth) * r.step;
                        }
                    }
                }
            }

            if (result.Any(v => !float.IsFinite(v)))
            {
                throw new InvalidDataException("Non-finite HOK dense samples.");
            }
            return result;
        }
    }

    public class ValueConstant
    {
        public uint m_ID;
        public uint m_TypeID;
        public uint m_Type;
        public uint m_Index;

        public ValueConstant(ObjectReader reader)
        {
            var version = reader.version;
            m_ID = reader.ReadUInt32();
            if (version[0] < 5 || (version[0] == 5 && version[1] < 5))//5.5 down
            {
                m_TypeID = reader.ReadUInt32();
            }
            m_Type = reader.ReadUInt32();
            m_Index = reader.ReadUInt32();
        }
    }

    public class ValueArrayConstant
    {
        public List<ValueConstant> m_ValueArray;

        public ValueArrayConstant(ObjectReader reader)
        {
            int numVals = reader.ReadInt32();
            m_ValueArray = new List<ValueConstant>();
            for (int i = 0; i < numVals; i++)
            {
                m_ValueArray.Add(new ValueConstant(reader));
            }
        }
    }

    public class Clip
    {
        public ACLClip m_ACLClip = new EmptyACLClip();
        public StreamedClip m_StreamedClip;
        public DenseClip m_DenseClip;
        public ConstantClip m_ConstantClip;
        public ValueArrayConstant m_Binding;
        public byte[] m_HokUnknownExtension;
        public Clip() { }

        public Clip(ObjectReader reader)
        {
            var version = reader.version;
            m_StreamedClip = new StreamedClip(reader);
            if (reader.Game.Type.IsArknightsEndfield() || reader.Game.Type.IsExAstris())
            {
                m_DenseClip = new ACLDenseClip(reader);
            }
            else
            {
                m_DenseClip = new DenseClip(reader);
            }
            if (reader.Game.Type.IsSRGroup())
            {
                m_ACLClip = new MHYACLClip();
                m_ACLClip.Read(reader);
            }
            if (version[0] > 4 || (version[0] == 4 && version[1] >= 3)) //4.3 and up
            {
                m_ConstantClip = new ConstantClip(reader);
            }
            if (reader.Game.Type.IsGIGroup() || reader.Game.Type.IsBH3Group() || reader.Game.Type.IsZZZCB1())
            {
                m_ACLClip = new MHYACLClip();
                m_ACLClip.Read(reader);
            }
            if (reader.Game.Type.IsLoveAndDeepspace())
            {
                m_ACLClip = new LnDACLClip();
                m_ACLClip.Read(reader);
            }
            if (version[0] < 2018 || (version[0] == 2018 && version[1] < 3)) //2018.3 down
            {
                m_Binding = new ValueArrayConstant(reader);
            }
            if (HokAnimationLayout.Applies(reader))
            {
                m_HokUnknownExtension = reader.ReadBytes(36);
            }
        }

        public static Clip ParseGI(ObjectReader reader)
        {
            var clipOffset = reader.Position + reader.ReadInt64();
            if (clipOffset > reader.Length)
            {
                throw new IOException("Offset outside of range");
            }

            var pos = reader.Position;
            reader.Position = clipOffset;

            var clip = new Clip();
            clip.m_StreamedClip = StreamedClip.ParseGI(reader);
            clip.m_DenseClip = DenseClip.ParseGI(reader);
            clip.m_ConstantClip = ConstantClip.ParseGI(reader);
            clip.m_ACLClip = new GIACLClip();
            clip.m_ACLClip.Read(reader);

            reader.Position = pos;

            return clip;
        }

        public AnimationClipBindingConstant ConvertValueArrayToGenericBinding()
        {
            var bindings = new AnimationClipBindingConstant();
            var genericBindings = new List<GenericBinding>();
            var values = m_Binding;
            for (int i = 0; i < values.m_ValueArray.Count;)
            {
                var curveID = values.m_ValueArray[i].m_ID;
                var curveTypeID = values.m_ValueArray[i].m_TypeID;
                var binding = new GenericBinding();
                genericBindings.Add(binding);
                if (curveTypeID == 4174552735) //CRC(PositionX))
                {
                    binding.path = curveID;
                    binding.attribute = 1; //kBindTransformPosition
                    binding.typeID = ClassIDType.Transform;
                    i += 3;
                }
                else if (curveTypeID == 2211994246) //CRC(QuaternionX))
                {
                    binding.path = curveID;
                    binding.attribute = 2; //kBindTransformRotation
                    binding.typeID = ClassIDType.Transform;
                    i += 4;
                }
                else if (curveTypeID == 1512518241) //CRC(ScaleX))
                {
                    binding.path = curveID;
                    binding.attribute = 3; //kBindTransformScale
                    binding.typeID = ClassIDType.Transform;
                    i += 3;
                }
                else
                {
                    binding.typeID = ClassIDType.Animator;
                    binding.path = 0;
                    binding.attribute = curveID;
                    i++;
                }
            }
            bindings.genericBindings = genericBindings;
            return bindings;
        }
    }

    public class ValueDelta
    {
        public float m_Start;
        public float m_Stop;

        public ValueDelta(ObjectReader reader)
        {
            m_Start = reader.ReadSingle();
            m_Stop = reader.ReadSingle();
        }
    }

    public class ClipMuscleConstant : IYAMLExportable
    {
        public HumanPose m_DeltaPose;
        public XForm m_StartX;
        public XForm m_StopX;
        public XForm m_LeftFootStartX;
        public XForm m_RightFootStartX;
        public XForm m_MotionStartX;
        public XForm m_MotionStopX;
        public Vector3 m_AverageSpeed;
        public Clip m_Clip;
        public float m_StartTime;
        public float m_StopTime;
        public float m_OrientationOffsetY;
        public float m_Level;
        public float m_CycleOffset;
        public float m_AverageAngularSpeed;
        public int[] m_IndexArray;
        public List<ValueDelta> m_ValueArrayDelta;
        public float[] m_ValueArrayReferencePose;
        public bool m_Mirror;
        public bool m_LoopTime;
        public bool m_LoopBlend;
        public bool m_LoopBlendOrientation;
        public bool m_LoopBlendPositionY;
        public bool m_LoopBlendPositionXZ;
        public bool m_StartAtOrigin;
        public bool m_KeepOriginalOrientation;
        public bool m_KeepOriginalPositionY;
        public bool m_KeepOriginalPositionXZ;
        public bool m_HeightFromFeet;
        public static bool HasShortIndexArray(SerializedType type) => type.Match("E708B1872AE48FD688AC012DF4A7A178") || type.Match("055AA41C7639327940F8900103A10356");
        public ClipMuscleConstant() { }

        public ClipMuscleConstant(ObjectReader reader)
        {
            var version = reader.version;
            if (reader.Game.Type.IsLoveAndDeepspace())
            {
                m_StartX = reader.ReadXForm();
                if (version[0] > 5 || (version[0] == 5 && version[1] >= 5))//5.5 and up
                {
                    m_StopX = reader.ReadXForm();
                }
            }
            else
            {
                m_DeltaPose = new HumanPose(reader);
                m_StartX = reader.ReadXForm();
                if (version[0] > 5 || (version[0] == 5 && version[1] >= 5))//5.5 and up
                {
                    m_StopX = reader.ReadXForm();
                }
                m_LeftFootStartX = reader.ReadXForm();
                m_RightFootStartX = reader.ReadXForm();
                if (version[0] < 5)//5.0 down
                {
                    m_MotionStartX = reader.ReadXForm();
                    m_MotionStopX = reader.ReadXForm();
                }
            }
            m_AverageSpeed = version[0] > 5 || (version[0] == 5 && version[1] >= 4) ? reader.ReadVector3() : (Vector3)reader.ReadVector4();//5.4 and up
            m_Clip = new Clip(reader);
            m_StartTime = reader.ReadSingle();
            m_StopTime = reader.ReadSingle();
            m_OrientationOffsetY = reader.ReadSingle();
            m_Level = reader.ReadSingle();
            m_CycleOffset = reader.ReadSingle();
            m_AverageAngularSpeed = reader.ReadSingle();

            if (reader.Game.Type.IsSR() && HasShortIndexArray(reader.serializedType))
            {
                m_IndexArray = reader.ReadInt16Array().Select(x => (int)x).ToArray();
            }
            else
            {
                m_IndexArray = reader.ReadInt32Array();
            }
            if (version[0] < 4 || (version[0] == 4 && version[1] < 3)) //4.3 down
            {
                var m_AdditionalCurveIndexArray = reader.ReadInt32Array();
            }
            int numDeltas = reader.ReadInt32();
            m_ValueArrayDelta = new List<ValueDelta>();
            for (int i = 0; i < numDeltas; i++)
            {
                m_ValueArrayDelta.Add(new ValueDelta(reader));
            }
            if (version[0] > 5 || (version[0] == 5 && version[1] >= 3))//5.3 and up
            {
                m_ValueArrayReferencePose = reader.ReadSingleArray();
            }

            m_Mirror = reader.ReadBoolean();
            if (version[0] > 4 || (version[0] == 4 && version[1] >= 3)) //4.3 and up
            {
                m_LoopTime = reader.ReadBoolean();
            }
            m_LoopBlend = reader.ReadBoolean();
            m_LoopBlendOrientation = reader.ReadBoolean();
            m_LoopBlendPositionY = reader.ReadBoolean();
            m_LoopBlendPositionXZ = reader.ReadBoolean();
            if (version[0] > 5 || (version[0] == 5 && version[1] >= 5))//5.5 and up
            {
                m_StartAtOrigin = reader.ReadBoolean();
            }
            m_KeepOriginalOrientation = reader.ReadBoolean();
            m_KeepOriginalPositionY = reader.ReadBoolean();
            m_KeepOriginalPositionXZ = reader.ReadBoolean();
            m_HeightFromFeet = reader.ReadBoolean();
            reader.AlignStream();
        }
        public static ClipMuscleConstant ParseGI(ObjectReader reader)
        {
            var version = reader.version;
            var clipMuscleConstant = new ClipMuscleConstant();

            clipMuscleConstant.m_DeltaPose = HumanPose.ParseGI(reader);
            clipMuscleConstant.m_StartX = reader.ReadXForm4();
            clipMuscleConstant.m_StopX = reader.ReadXForm4();
            clipMuscleConstant.m_LeftFootStartX = reader.ReadXForm4();
            clipMuscleConstant.m_RightFootStartX = reader.ReadXForm4();

            clipMuscleConstant.m_AverageSpeed = (Vector3)reader.ReadVector4();

            clipMuscleConstant.m_Clip = Clip.ParseGI(reader);

            clipMuscleConstant.m_StartTime = reader.ReadSingle();
            clipMuscleConstant.m_StopTime = reader.ReadSingle();
            clipMuscleConstant.m_OrientationOffsetY = reader.ReadSingle();
            clipMuscleConstant.m_Level = reader.ReadSingle();
            clipMuscleConstant.m_CycleOffset = reader.ReadSingle();
            clipMuscleConstant.m_AverageAngularSpeed = reader.ReadSingle();

            clipMuscleConstant.m_IndexArray = reader.ReadInt16Array(0xC8).Select(x => (int)x).ToArray();

            var valueArrayDeltaCount = (int)reader.ReadUInt64();
            var valueArrayDeltaOffset = reader.Position + reader.ReadInt64();

            if (valueArrayDeltaOffset > reader.Length)
            {
                throw new IOException("Offset outside of range");
            }

            var valueArrayReferencePoseCount = (int)reader.ReadUInt64();
            var valueArrayReferencePoseOffset = reader.Position + reader.ReadInt64();

            if (valueArrayReferencePoseOffset > reader.Length)
            {
                throw new IOException("Offset outside of range");
            }

            clipMuscleConstant.m_Mirror = reader.ReadBoolean();
            clipMuscleConstant.m_LoopTime = reader.ReadBoolean();
            clipMuscleConstant.m_LoopBlend = reader.ReadBoolean();
            clipMuscleConstant.m_LoopBlendOrientation = reader.ReadBoolean();
            clipMuscleConstant.m_LoopBlendPositionY = reader.ReadBoolean();
            clipMuscleConstant.m_LoopBlendPositionXZ = reader.ReadBoolean();
            clipMuscleConstant.m_StartAtOrigin = reader.ReadBoolean();
            clipMuscleConstant.m_KeepOriginalOrientation = reader.ReadBoolean();
            clipMuscleConstant.m_KeepOriginalPositionY = reader.ReadBoolean();
            clipMuscleConstant.m_KeepOriginalPositionXZ = reader.ReadBoolean();
            clipMuscleConstant.m_HeightFromFeet = reader.ReadBoolean();
            reader.AlignStream();

            if (valueArrayDeltaCount > 0)
            {
                reader.Position = valueArrayDeltaOffset;
                clipMuscleConstant.m_ValueArrayDelta = new List<ValueDelta>();
                for (int i = 0; i < valueArrayDeltaCount; i++)
                {
                    clipMuscleConstant.m_ValueArrayDelta.Add(new ValueDelta(reader));
                }
            }

            if (valueArrayReferencePoseCount > 0)
            {
                reader.Position = valueArrayReferencePoseOffset;
                clipMuscleConstant.m_ValueArrayReferencePose = reader.ReadSingleArray(valueArrayReferencePoseCount);
            }

            return clipMuscleConstant;
        }
        public YAMLNode ExportYAML(int[] version)
        {
            var node = new YAMLMappingNode();
            node.AddSerializedVersion(ToSerializedVersion(version));
            node.Add(nameof(m_StartTime), m_StartTime);
            node.Add(nameof(m_StopTime), m_StopTime);
            node.Add(nameof(m_OrientationOffsetY), m_OrientationOffsetY);
            node.Add(nameof(m_Level), m_Level);
            node.Add(nameof(m_CycleOffset), m_CycleOffset);
            node.Add(nameof(m_LoopTime), m_LoopTime);
            node.Add(nameof(m_LoopBlend), m_LoopBlend);
            node.Add(nameof(m_LoopBlendOrientation), m_LoopBlendOrientation);
            node.Add(nameof(m_LoopBlendPositionY), m_LoopBlendPositionY);
            node.Add(nameof(m_LoopBlendPositionXZ), m_LoopBlendPositionXZ);
            node.Add(nameof(m_KeepOriginalOrientation), m_KeepOriginalOrientation);
            node.Add(nameof(m_KeepOriginalPositionY), m_KeepOriginalPositionY);
            node.Add(nameof(m_KeepOriginalPositionXZ), m_KeepOriginalPositionXZ);
            node.Add(nameof(m_HeightFromFeet), m_HeightFromFeet);
            node.Add(nameof(m_Mirror), m_Mirror);
            return node;
        }
        private int ToSerializedVersion(int[] version)
        {
            if (version[0] > 5 || (version[0] == 5 && version[1] >= 6))
            {
                return 3;
            }
            else if (version[0] > 4 || (version[0] == 4 && version[1] >= 3))
            {
                return 2;
            }
            return 1;
        }
    }

    public class GenericBinding : IYAMLExportable
    {
        public int[] version;
        public uint path;
        public uint attribute;
        public PPtr<Object> script;
        public ClassIDType typeID;
        public byte customType;
        public byte isPPtrCurve;
        public byte isIntCurve;

        public GenericBinding() { }

        public GenericBinding(ObjectReader reader)
        {
            version = reader.version;
            path = reader.ReadUInt32();
            attribute = reader.ReadUInt32();
            script = new PPtr<Object>(reader);
            if (version[0] > 5 || (version[0] == 5 && version[1] >= 6)) //5.6 and up
            {
                typeID = (ClassIDType)reader.ReadInt32();
            }
            else
            {
                typeID = (ClassIDType)reader.ReadUInt16();
            }
            customType = reader.ReadByte();
            isPPtrCurve = reader.ReadByte();
            if (version[0] > 2022 || (version[0] == 2022 && version[1] >= 1)) //2022.1 and up
            {
                isIntCurve = reader.ReadByte();
            }
            reader.AlignStream();
        }

        public YAMLNode ExportYAML(int[] version)
        {
            var node = new YAMLMappingNode();
            node.Add(nameof(path), path);
            node.Add(nameof(attribute), attribute);
            node.Add(nameof(script), script.ExportYAML(version));
            node.Add("classID", ((int)typeID).ToString());
            node.Add(nameof(customType), customType);
            node.Add(nameof(isPPtrCurve), isPPtrCurve);
            return node;
        }
    }

    public class AnimationClipBindingConstant : IYAMLExportable
    {
        public List<GenericBinding> genericBindings;
        public List<PPtr<Object>> pptrCurveMapping;

        public AnimationClipBindingConstant() { }

        public AnimationClipBindingConstant(ObjectReader reader)
        {
            int numBindings = reader.ReadInt32();
            genericBindings = new List<GenericBinding>();
            for (int i = 0; i < numBindings; i++)
            {
                genericBindings.Add(new GenericBinding(reader));
            }

            int numMappings = reader.ReadInt32();
            pptrCurveMapping = new List<PPtr<Object>>();
            for (int i = 0; i < numMappings; i++)
            {
                pptrCurveMapping.Add(new PPtr<Object>(reader));
            }
        }

        public YAMLNode ExportYAML(int[] version)
        {
            var node = new YAMLMappingNode();
            node.Add(nameof(genericBindings), genericBindings.ExportYAML(version));
            node.Add(nameof(pptrCurveMapping), pptrCurveMapping.ExportYAML(version));
            return node;
        }

        public GenericBinding FindBinding(int index)
        {
            int curves = 0;
            foreach (var b in genericBindings)
            {
                if (b.typeID == ClassIDType.Transform)
                {
                    switch (b.attribute)
                    {
                        case 1: //kBindTransformPosition
                        case 3: //kBindTransformScale
                        case 4: //kBindTransformEuler
                            curves += 3;
                            break;
                        case 2: //kBindTransformRotation
                            curves += 4;
                            break;
                        default:
                            curves += 1;
                            break;
                    }
                }
                else
                {
                    curves += 1;
                }
                if (curves > index)
                {
                    return b;
                }
            }

            return null;
        }
    }

    public class AnimationEvent : IYAMLExportable
    {
        public float time;
        public string functionName;
        public string data;
        public PPtr<Object> objectReferenceParameter;
        public float floatParameter;
        public int intParameter;
        public int messageOptions;

        public AnimationEvent(ObjectReader reader)
        {
            var version = reader.version;

            time = reader.ReadSingle();
            functionName = reader.ReadAlignedString();
            data = reader.ReadAlignedString();
            objectReferenceParameter = new PPtr<Object>(reader);
            floatParameter = reader.ReadSingle();
            if (version[0] >= 3) //3 and up
            {
                intParameter = reader.ReadInt32();
            }
            messageOptions = reader.ReadInt32();
        }

        public YAMLNode ExportYAML(int[] version)
        {
            var node = new YAMLMappingNode();
            node.Add(nameof(time), time);
            node.Add(nameof(functionName), functionName);
            node.Add(nameof(data), data);
            node.Add(nameof(objectReferenceParameter), objectReferenceParameter.ExportYAML(version));
            node.Add(nameof(floatParameter), floatParameter);
            node.Add(nameof(intParameter), intParameter);
            node.Add(nameof(messageOptions), messageOptions);
            return node;
        }
    }

    public enum AnimationType
    {
        Legacy = 1,
        Generic = 2,
        Humanoid = 3
    };

    public sealed class AnimationClip : NamedObject
    {
        public AnimationType m_AnimationType;
        public bool m_Legacy;
        public bool m_Compressed;
        public bool m_UseHighQualityCurve;
        public List<QuaternionCurve> m_RotationCurves;
        public List<CompressedAnimationCurve> m_CompressedRotationCurves;
        public List<Vector3Curve> m_EulerCurves;
        public List<Vector3Curve> m_PositionCurves;
        public List<Vector3Curve> m_ScaleCurves;
        public List<FloatCurve> m_FloatCurves;
        public List<PPtrCurve> m_PPtrCurves;
        public float m_SampleRate;
        public int m_WrapMode;
        public AABB m_Bounds;
        public HokLegacyAnimation m_HokLegacyAnimation;
        public uint m_MuscleClipSize;
        public ClipMuscleConstant m_MuscleClip;
        public AnimationClipBindingConstant m_ClipBindingConstant;
        public List<AnimationEvent> m_Events;
        public StreamingInfo m_StreamData;
        public byte[] m_HokUnknownTrailing;

        private bool hasStreamingInfo = false;

        public AnimationClip(ObjectReader reader) : base(reader)
        {
            if (version[0] >= 5)//5.0 and up
            {
                m_Legacy = reader.ReadBoolean();
            }
            else if (version[0] >= 4)//4.0 and up
            {
                m_AnimationType = (AnimationType)reader.ReadInt32();
                if (m_AnimationType == AnimationType.Legacy)
                    m_Legacy = true;
            }
            else
            {
                m_Legacy = true;
            }
            if (reader.Game.Type.IsLoveAndDeepspace())
            {
                reader.AlignStream();
                var m_aclTransformCache = reader.ReadUInt8Array();
                var m_aclScalarCache = reader.ReadUInt8Array();
                int numaclTransformTrackId2CurveId = reader.ReadInt32();
                var m_aclTransformTrackId2CurveId = new List<AclTransformTrackIDToBindingCurveID>();
                for (int i = 0; i < numaclTransformTrackId2CurveId; i++)
                {
                    m_aclTransformTrackId2CurveId.Add(new AclTransformTrackIDToBindingCurveID(reader));
                }
                var m_aclScalarTrackId2CurveId = reader.ReadUInt32Array();
            }
            m_Compressed = reader.ReadBoolean();
            if (version[0] > 4 || (version[0] == 4 && version[1] >= 3))//4.3 and up
            {
                m_UseHighQualityCurve = reader.ReadBoolean();
            }
            reader.AlignStream();
            int numRCurves = reader.ReadInt32();
            m_RotationCurves = new List<QuaternionCurve>();
            for (int i = 0; i < numRCurves; i++)
            {
                m_RotationCurves.Add(new QuaternionCurve(reader));
            }

            int numCRCurves = reader.ReadInt32();
            m_CompressedRotationCurves = new List<CompressedAnimationCurve>();
            for (int i = 0; i < numCRCurves; i++)
            {
                m_CompressedRotationCurves.Add(new CompressedAnimationCurve(reader));
            }

            if (reader.Game.Type.IsExAstris())
            {
                var m_aclType = reader.ReadInt32();
            }

            if (version[0] > 5 || (version[0] == 5 && version[1] >= 3))//5.3 and up
            {
                int numEulerCurves = reader.ReadInt32();
                m_EulerCurves = new List<Vector3Curve>();
                for (int i = 0; i < numEulerCurves; i++)
                {
                    m_EulerCurves.Add(new Vector3Curve(reader));
                }
            }

            int numPCurves = reader.ReadInt32();
            m_PositionCurves = new List<Vector3Curve>();
            for (int i = 0; i < numPCurves; i++)
            {
                m_PositionCurves.Add(new Vector3Curve(reader));
            }

            int numSCurves = reader.ReadInt32();
            m_ScaleCurves = new List<Vector3Curve>();
            for (int i = 0; i < numSCurves; i++)
            {
                m_ScaleCurves.Add(new Vector3Curve(reader));
            }

            int numFCurves = reader.ReadInt32();
            m_FloatCurves = new List<FloatCurve>();
            for (int i = 0; i < numFCurves; i++)
            {
                m_FloatCurves.Add(new FloatCurve(reader));
            }

            if (version[0] > 4 || (version[0] == 4 && version[1] >= 3)) //4.3 and up
            {
                int numPtrCurves = reader.ReadInt32();
                m_PPtrCurves = new List<PPtrCurve>();
                for (int i = 0; i < numPtrCurves; i++)
                {
                    m_PPtrCurves.Add(new PPtrCurve(reader));
                }
            }

            m_SampleRate = reader.ReadSingle();
            m_WrapMode = reader.ReadInt32();
            if (reader.Game.Type.IsArknightsEndfield())
            {
                var m_aclType = reader.ReadInt32();
            }
            if (version[0] > 3 || (version[0] == 3 && version[1] >= 4)) //3.4 and up
            {
                m_Bounds = new AABB(reader);
            }
            if (HokAnimationLayout.Applies(reader))
            {
                // HOK stores a self contained legacy animation block (its own Clip
                // plus a path/binding table) between m_Bounds and m_MuscleClipSize.
                m_HokLegacyAnimation = new HokLegacyAnimation(reader);
            }
            if (version[0] >= 4)//4.0 and up
            {
                if (reader.Game.Type.IsGI())
                {
                    var muscleClipSize = reader.ReadInt32();
                    if (muscleClipSize < 0)
                    {
                        hasStreamingInfo = true;
                        m_MuscleClipSize = reader.ReadUInt32();
                        var pos = reader.Position;
                        m_MuscleClip = ClipMuscleConstant.ParseGI(reader);
                        reader.Position = pos + m_MuscleClipSize;
                    }
                    else if (muscleClipSize > 0)
                    {
                        m_MuscleClipSize = (uint)muscleClipSize;
                        m_MuscleClip = new ClipMuscleConstant(reader);
                    }
                }
                else
                {
                    m_MuscleClipSize = reader.ReadUInt32();
                    m_MuscleClip = new ClipMuscleConstant(reader);
                }
            }
            if (reader.Game.Type.IsSRGroup())
            {
                var m_AclClipData = reader.ReadUInt8Array();
                var aclBindingsCount = reader.ReadInt32();
                var m_AclBindings = new List<GenericBinding>();
                for (int i = 0; i < aclBindingsCount; i++)
                {
                    m_AclBindings.Add(new GenericBinding(reader));
                }
                var m_AclRange = new KeyValuePair<float, float>(reader.ReadSingle(), reader.ReadSingle());
            }
            if (version[0] > 4 || (version[0] == 4 && version[1] >= 3)) //4.3 and up
            {
                m_ClipBindingConstant = new AnimationClipBindingConstant(reader);
            }
            if (version[0] > 2018 || (version[0] == 2018 && version[1] >= 3)) //2018.3 and up
            {
                var m_HasGenericRootTransform = reader.ReadBoolean();
                var m_HasMotionFloatCurves = reader.ReadBoolean();
                reader.AlignStream();
            }
            int numEvents = reader.ReadInt32();
            m_Events = new List<AnimationEvent>();
            for (int i = 0; i < numEvents; i++)
            {
                m_Events.Add(new AnimationEvent(reader));
            }
            if (version[0] >= 2017) //2017 and up
            {
                reader.AlignStream();
            }
            if (HokAnimationLayout.Applies(reader))
            {
                long remaining = reader.byteStart + reader.byteSize - reader.Position;
                if (remaining < 0 || remaining > int.MaxValue)
                {
                    throw new InvalidDataException("HOK animation crossed object boundary.");
                }
                m_HokUnknownTrailing = reader.ReadBytes((int)remaining);
            }
            if (hasStreamingInfo)
            {
                m_StreamData = new StreamingInfo(reader);
                if (!string.IsNullOrEmpty(m_StreamData?.path))
                {
                    var aclClip = m_MuscleClip.m_Clip.m_ACLClip as GIACLClip;

                    var resourceReader = new ResourceReader(m_StreamData.path, assetsFile, m_StreamData.offset, m_StreamData.size);
                    using var ms = new MemoryStream();
                    ms.Write(aclClip.m_DatabaseData);

                    ms.Write(resourceReader.GetData());
                    ms.AlignStream();

                    aclClip.m_DatabaseData = ms.ToArray();
                }
            }
            if (HokAnimationLayout.Applies(reader) && m_HokLegacyAnimation != null)
            {
                // HOK legacy clips keep every curve inside the embedded block, the
                // regular curve lists on disk are empty. Rebuild them so the
                // YAML/FBX exporters can emit the animation.
                m_HokLegacyAnimation.BuildCurves(m_RotationCurves, m_PositionCurves, m_ScaleCurves, m_EulerCurves, m_FloatCurves);
            }
        }
    }
}

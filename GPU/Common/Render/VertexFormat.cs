using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.GPU.Render
{
	public enum VertexFormatParameter
    {
		Position2f,
        UV2f
    }

    public struct VertexFormatParameterEntry
    {
		public VertexFormatParameter Parameter { get; set; }
		public string ParameterName { get; set; }
		public uint ByteOffset { get; set; }
		public VertexFormatParameterEntry(VertexFormatParameter parameter, string parameterName, uint byteOffset)
        {
			ParameterName = parameterName;
			Parameter = parameter;
			ByteOffset = byteOffset;
        }

		public uint ComponentCount
		{
			get
			{
				switch (Parameter)
				{
					case VertexFormatParameter.Position2f:
					case VertexFormatParameter.UV2f:
						return 2;
					default:
						throw new Exception("Unsupported vertex format parameter");
				}
			}
		}

        public bool IsNormalised 
		{
			get
			{
				switch (Parameter)
				{
					case VertexFormatParameter.Position2f:
					case VertexFormatParameter.UV2f:
						return false;
					default:
						throw new Exception("Unsupported vertex format parameter");
				}
			}
		}
	}

	public class VertexFormat
	{
		public uint VertexSizeBytes { get; private set; }
		public List<VertexFormatParameterEntry> Parameters { get; private set; }

		public VertexFormat()
        {
			Parameters = new List<VertexFormatParameterEntry>();
		}

		uint ParameterSizeBytes(VertexFormatParameter parameter)
		{
			switch (parameter)
			{
				case VertexFormatParameter.Position2f:
				case VertexFormatParameter.UV2f:
					return 8;
				default:
					throw new Exception("Unsupported vertex format parameter");
			}
		}

		public void AddParameter(VertexFormatParameter parameter, string parameterName)
        {
			// Create the parameter entry
			VertexFormatParameterEntry entry = new VertexFormatParameterEntry(parameter, parameterName, VertexSizeBytes);

			// Get the size of the parameter and save the new size of the vertex
			Parameters.Add(entry);
			VertexSizeBytes += ParameterSizeBytes(parameter);
		}

		public void Clear()
        {
			Parameters.Clear();
			VertexSizeBytes = 0;
		}
    }
}

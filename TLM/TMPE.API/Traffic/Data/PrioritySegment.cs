using System;
using TrafficManager.Traffic.Enums;

namespace TrafficManager.Traffic.Data {
	using API.Traffic.Enums;

	/// <summary>
	/// A priority segment specifies the priority signs that are present at each end of a certain segment.
	/// </summary>
	public struct PrioritySegment {
		/// <summary>
		/// Priority sign at start node (default: None)
		/// </summary>
		public PriorityType startType;

		/// <summary>
		/// Priority sign at end node (default: None)
		/// </summary>
		public PriorityType endType;

		public override string ToString() {
			return $"[PrioritySegment\n" +
				"\t" + $"startType = {startType}\n" +
				"\t" + $"endType = {endType}\n" +
				"PrioritySegment]";
		}

		public PrioritySegment(PriorityType startType, PriorityType endType) {
			this.startType = startType;
			this.endType = endType;
		}

		public void Reset() {
			startType = PriorityType.None;
			endType = PriorityType.None;
		}

		public bool IsDefault() {
			return !HasPrioritySignAtNode(true) && !HasPrioritySignAtNode(false);
		}

		public bool HasPrioritySignAtNode(bool startNode) {
			if (startNode) {
				return startType != PriorityType.None;
			} else {
				return endType != PriorityType.None;
			}
		}
	}
}

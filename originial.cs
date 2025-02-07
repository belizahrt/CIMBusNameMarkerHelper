public class TopoNodeMarkersMapper
{
      	private volatile static IExtendedDataSourceService DataSource = 
      		ServiceLocator.Current.GetInstance<IExtendedDataSourceService>();	
	private volatile static IModelImage ModelImage = DataSource.MainModelImage as ModelImage;

       private int BaseNumber = 10000;
       private int NextNumber
       {
       	get
       	{	
       		var busyNumbers = ModelImage.GetObjectsEMS<TopoNodeMarker>()
       			.Select(tnm => tnm.number).ToHashSet();
       			
       		int n = BaseNumber;
       		while (busyNumbers.Contains(n)) { ++n; }
       		return n;
       	}
       }
       
      private HashSet<(int, int)> AssignedRetainedBranches = new HashSet<(int, int)>();
      private HashSet<ConnectivityNodeContainer> LTContainers = new HashSet<ConnectivityNodeContainer>();
      
      public TopoNodeMarkersMapper()
      {
      }
      
      public void ClearRetained()
      {
      		ModelImage.GetObjects<Switch>().ToList().ForEach(s => s.retained = false);
      }
      
      public void Map(int baseNumber = 10000, ConnectivityNodeContainer boundContainer = null)
      {
      		BaseNumber = baseNumber;
      		
      		AssignedRetainedBranches.Clear();
      		LTContainers.Clear();
      		
      		try
      		{
	      		MapRegulatingCondEq(boundContainer);
			MapEquivalentInjection(boundContainer);
			MapBusbarSection(boundContainer);
			MapJunction(boundContainer);
			MapConformLoad(boundContainer);
			MapNonMarkedNodes(boundContainer);
			MapACLineSegment(boundContainer);
			MapSeriesCompensator(boundContainer);
			MapPowerTransformer(boundContainer);
			MapLineOrTransformerBayComposite();	
			AssignRetained(boundContainer);
		}
		catch(Exception ex)
		{
			ProtocolHelper.Publish($"Возникла неизвестная ошибка: \n {ex.Message}",
				"Назначение базовых узлов");
		}
		
		System.Windows.MessageBox.Show("Операция завершена");
     	}
      
      	private void MapRegulatingCondEq(ConnectivityNodeContainer boundContainer = null)
      	{
      		IEnumerable<ConnectivityNode> connectivityNodes; 
      		
      		if (boundContainer == null)
      		{
      			connectivityNodes = ModelImage.GetObjects<ConnectivityNode>();
      		}
      		else
      		{
      			connectivityNodes = GetChildrenOfType<ConnectivityNode>(boundContainer, true);
      		}
      	
        	var rceCN = connectivityNodes
			.Where(cn => cn.Terminals.Any(t => t.ConductingEquipment is RegulatingCondEq));
			
		var retainedRCE = new HashSet<Switch>();
		
		foreach (var cn in rceCN)
		{	
			var filter = cn.Terminals
				.Where(t => t.ConductingEquipment is RegulatingCondEq);
				
			var rce = filter.First().ConductingEquipment as RegulatingCondEq;
			AssignRetainedRCE(rce);

			int number = NextNumber;
			int priority = 1;

			foreach (var t in filter)
			{	
				if (t.BusNameMarker != null)
				{
					continue;
				}
			
				AssignBusNameMarker(t, number, priority++, "MapRegulatingCondEq");	
			}
		}    		
      	}
      	
      	private void MapEquivalentInjection(ConnectivityNodeContainer boundContainer = null)
      	{
      		IEnumerable<ConnectivityNode> connectivityNodes; 
      		
      		if (boundContainer == null)
      		{
      			connectivityNodes = ModelImage.GetObjects<ConnectivityNode>();
      		}
      		else
      		{
      			connectivityNodes = GetChildrenOfType<ConnectivityNode>(boundContainer, true);
      		}
      	
		var eiCN = connectivityNodes
			.Where(cn => cn.Terminals.Any(t => t.ConductingEquipment is EquivalentInjection));
			
		foreach (var cn in eiCN)
		{
			if (cn.Terminals.Any(t => t.BusNameMarker != null))
			{
				continue;
			}
		
			int number = NextNumber;
			int priority = 1;
			foreach (var t in cn.Terminals
				.Where(t => t.ConductingEquipment is EquivalentInjection))
			{
				AssignBusNameMarker(t, number, priority++, "MapEquivalentInjection");		
			}
		}		
      	}

      	private void MapBusbarSection(ConnectivityNodeContainer boundContainer = null)
      	{
      		IEnumerable<ConnectivityNode> connectivityNodes; 
      		
      		if (boundContainer == null)
      		{
      			connectivityNodes = ModelImage.GetObjects<ConnectivityNode>();
      		}
      		else
      		{
      			connectivityNodes = GetChildrenOfType<ConnectivityNode>(boundContainer, true);
      		}
      	
      		var bbsCN = connectivityNodes
			.Where(cn => cn.Terminals.Any(t => t.ConductingEquipment is BusbarSection 
				&& (t.ConductingEquipment as BusbarSection).busbarSystemKind != BusbarSystemKind.transfer));
			
		foreach (var cn in bbsCN)
		{
			if (cn.Terminals.Any(t => t.BusNameMarker != null))
			{
				continue;
			}
		
			int number = NextNumber;
			int priority = 1;
			foreach (var t in cn.Terminals
				.Where(t => t.ConductingEquipment is BusbarSection))
			{
				AssignBusNameMarker(t, number, priority++, "MapBusbarSection");
			}
		}
      	}
      	
      	private void MapJunction(ConnectivityNodeContainer boundContainer = null)
      	{      	
      		IEnumerable<ConnectivityNode> connectivityNodes; 
      		
      		if (boundContainer == null)
      		{
      			connectivityNodes = ModelImage.GetObjects<ConnectivityNode>();
      		}
      		else
      		{
      			connectivityNodes = GetChildrenOfType<ConnectivityNode>(boundContainer, true);
      		}
      	
      		var junctionCN = connectivityNodes
			.Where(cn => cn.Terminals.Any(t => t.ConductingEquipment is Junction));
			
		foreach (var cn in junctionCN)
		{
			if (TopologyProcessor.FindTopoNodeZW(cn) != null)
			{
				continue;
			}
		
			int number = NextNumber;
			int priority = 1;
			foreach (var t in cn.Terminals
				.Where(t => t.ConductingEquipment is Junction))
			{
				AssignBusNameMarker(t, NextNumber, priority++, "MapJunction");
			}
		}
	}
	
	private void MapConformLoad(ConnectivityNodeContainer boundContainer = null)
	{
      		IEnumerable<ConnectivityNode> connectivityNodes; 
      		
      		if (boundContainer == null)
      		{
      			connectivityNodes = ModelImage.GetObjects<ConnectivityNode>();
      		}
      		else
      		{
      			connectivityNodes = GetChildrenOfType<ConnectivityNode>(boundContainer, true);
      		}
      	
		var loadsCN = connectivityNodes
			.Where(cn => cn.Terminals.Any(t => t.ConductingEquipment is ConformLoad));
			
		foreach (var cn in loadsCN)
		{
			if (TopologyProcessor.FindTopoNodeZW(cn) != null)
			{
				continue;
			}
		
			int number = NextNumber;
			int priority = 1;
			foreach (var t in cn.Terminals
				.Where(t => t.ConductingEquipment is ConformLoad))
			{
				AssignBusNameMarker(t, NextNumber, priority++, "MapConformLoad");
			}
		}
	}
		
	private void MapNonMarkedNodes(ConnectivityNodeContainer boundContainer = null)
	{
      		IEnumerable<ConnectivityNode> connectivityNodes; 
      		
      		if (boundContainer == null)
      		{
      			connectivityNodes = ModelImage.GetObjects<ConnectivityNode>();
      		}
      		else
      		{
      			connectivityNodes = GetChildrenOfType<ConnectivityNode>(boundContainer, true);
      		}

		var nonMarkedNodes =connectivityNodes;
		
		foreach (var cn in nonMarkedNodes)
		{
			if (!cn.Terminals.All(t => t.BusNameMarker == null))
			{
				continue;
			}
		
			if (cn.Terminals.Any(x => x.ConductingEquipment is BusbarSection
				|| x.ConductingEquipment is ACLineSegment))
			{
				continue;
			}
		
			var bts = cn.Terminals.Where(t => t.ConductingEquipment.Terminals.Count() >= 2);
			if (bts.Count() >= 3)
			{
				bool check = true;
				foreach (var bt in bts)
				{
					if (bt.ConductingEquipment.ClassId != bts.First().ConductingEquipment.ClassId)
					{
						check = false;
						break;
					}
				}
				
				if (check == true)
				{
					int number = NextNumber;
					int priority = 1;
					foreach (var t in cn.Terminals)
					{	
						AssignBusNameMarker(t, number, priority++, "MapNonMarkedNodes");
					}
				}
			}
		/*
			if (TopologyProcessor.FindSwitchAround(cn).Select(s => s.EquipmentContainer)
					.Distinct().Count() < 3
				|| !cn.Terminals.All(t => t.BusNameMarker == null))
			{
				continue;
			}
			
			var bbs = TopologyProcessor.FindTopoOfTypeZW<BusbarSection>(cn);
			if (bbs != null && bbs.busbarSystemKind == BusbarSystemKind.transfer)
			{
				continue;
			}
			
			if (cn.Terminals.Select(t => t.ConductingEquipment)
				.Any(e =>  e.BaseVoltage == null ? true : e.BaseVoltage.nominalVoltage < 110))
			{
				continue;
			}
			
			int number = NextNumber;
			int priority = 1;
			foreach (var t in cn.Terminals)
			{	
				AssignBusNameMarker(t, NextNumber, priority++);
			}
			*/
		}	
	}
		
	private void MapACLineSegment(ConnectivityNodeContainer boundContainer = null)
	{
      		IEnumerable<ConnectivityNode> connectivityNodes; 
      		
      		if (boundContainer == null)
      		{
      			connectivityNodes = ModelImage.GetObjects<ConnectivityNode>();
      		}
      		else
      		{
      			connectivityNodes = GetChildrenOfType<ConnectivityNode>(boundContainer, true);
      		}
      	
		var acsegmentsCN = connectivityNodes
			.Where(cn => cn.Terminals.Any(t => t.ConductingEquipment is ACLineSegment));
		
		foreach (var cn in acsegmentsCN)
		{
			TopologyProcessor.FindSwitchAround(cn)
				.Select(s => s.EquipmentContainer).ToList()
				.ForEach(ec => { if (!LTContainers.Contains(ec)) LTContainers.Add(ec); });
		
			if (TopologyProcessor.FindTopoNodeZW(cn) != null)
			{
				continue;
			}
		
			int number = NextNumber;
			int priority = 1;
			foreach (var t in cn.Terminals)
			{	
				AssignBusNameMarker(t, number, priority++, "MapACLineSegment");
			}
		}
	}
		
	private void MapSeriesCompensator(ConnectivityNodeContainer boundContainer = null)
	{
      		IEnumerable<SeriesCompensator> seriesCompensors; 
      		
      		if (boundContainer == null)
      		{
      			seriesCompensors = ModelImage.GetObjects<SeriesCompensator>();
      		}
      		else
      		{
      			seriesCompensors = GetChildrenOfType<SeriesCompensator>(boundContainer, true);
      		}
      	
		var seriesCompensators = seriesCompensors;
		
		foreach (var sc in seriesCompensators)
		{
			foreach (var t in sc.Terminals)
			{
				if (TopologyProcessor.FindTopoNodeZW(t.ConnectivityNode) != null)
				{	
					continue;
				}
				
				AssignBusNameMarker(t, NextNumber, 1, "MapSeriesCompensator");	
			}
		}
	}
		
	private void MapPowerTransformer(ConnectivityNodeContainer boundContainer = null)
	{
	      	IEnumerable<PowerTransformer> trans; 
      		
      		if (boundContainer == null)
      		{
      			trans = ModelImage.GetObjects<PowerTransformer>();
      		}
      		else
      		{
      			trans = GetChildrenOfType<PowerTransformer>(boundContainer, true);
      		}
	
		var mpteTransformers = trans;
			
		foreach (var pt in mpteTransformers)
		{
			if (pt.PowerTransformerEnd.Count() > 2
				&& pt.StarCenterMarker == null
				&& pt.ControlArea != null)
			{
				StarCenterMarker scm = ModelImage.CreateObject<StarCenterMarker>();
				
				scm.PowerTransformer = pt;
				scm.number = NextNumber;
			}
			
			foreach (var pte in pt.PowerTransformerEnd)
			{
				TopologyProcessor.FindSwitchAround(pte.Terminal.ConnectivityNode)
					.Select(s => s.EquipmentContainer).ToList()
					.ForEach(ec => { if (!LTContainers.Contains(ec)) LTContainers.Add(ec); });
			
				if (TopologyProcessor.FindTopoNodeZW(pte.Terminal.ConnectivityNode) != null)
				{
					continue;
				}
				
				AssignBusNameMarker(pte.Terminal, NextNumber, 1, "MapPowerTransformer");		
			}
		}
	}
		
	private void MapLineOrTransformerBayComposite()
	{
		foreach (var container in LTContainers)
		{
			var openSwitches = GetChildrenOfType<Switch>(container, true)
				.Where(s => s.Terminals.Count() == 2 && s.normalOpen == true);
				
			Switch s = openSwitches
				.OfType<Breaker>().FirstOrDefault();
							
			if (s == null)
			{
				s = openSwitches
					.OfType<Disconnector>().FirstOrDefault();
								
				if (s == null)
				{
					continue;
				}
			}
				
			
			foreach (var t in s.Terminals)
			{
				//try{
				if (TopologyProcessor.FindTopoNodeZW(t.ConnectivityNode) != null)
				{	
					continue;
				}
					
				var bbs = TopologyProcessor.FindTopoOfTypeZW<BusbarSection>(t.ConnectivityNode);
				if (bbs != null && bbs.busbarSystemKind == BusbarSystemKind.transfer)
				{
					continue;
				}
					
				AssignBusNameMarker(t, NextNumber, 5, "MapLineOrTransformerBayComposite");	
					//}catch(Exception) {continue;}
			}
		}
	}
		
	private void AssignRetained(ConnectivityNodeContainer boundContainer = null)
	{
		IEnumerable<Switch> sws; 
      		
      		if (boundContainer == null)
      		{
      			sws = ModelImage.GetObjects<Switch>();
      		}
      		else
      		{
      			sws = GetChildrenOfType<Switch>(boundContainer, true);
      		}
	
		var assignedRetainedBranches = new HashSet<(int, int)>();
		
		var switches = sws.Where(s => s.Terminals.Count() == 2);
		var breakers = switches.Where(s => s is Breaker);
		var restSwitches = switches.Where(s => !(s is Breaker) && !(s is Jumper)); 
		
		breakers.ToList().ForEach(s => AssignSwitchRetained(s));
		restSwitches.ToList().ForEach(s => AssignSwitchRetained(s));
	}

	public void AssignBusNameMarker(Terminal terminal, int number, int priority, string reason)
	{
		if (terminal.ConductingEquipment.ControlArea == null)
		{
			return;
		}
	
		BusNameMarker bnm = ModelImage.CreateObject<BusNameMarker>();
		bnm.number = number;
		bnm.priority = priority;
			
		terminal.BusNameMarker = bnm;	
		
		ProtocolHelper.Publish($"Назначен полюсный маркер на Т{terminal.sequenceNumber} ({terminal.ConductingEquipment.name})",
			$"Назначение базовых узлов: {reason}");
	}

	private void AssignRetainedRCE(RegulatingCondEq rce)
	{
		var cn = rce.Terminals.First().ConnectivityNode;
			
		var sw = TopologyProcessor.FindTopoOfTypeZW<Switch>(cn);
			
		if (sw == null)
		{
			return;
		}
				
		var bay = sw.EquipmentContainer as Bay;
				
		if (bay == null)
		{
			sw.retained = true;
			return;
		}	
				
		Switch s = bay.Equipments
			.OfType<Breaker>().FirstOrDefault();
						
		if (s == null)
		{
			s = bay.Equipments
				.OfType<Disconnector>().FirstOrDefault();
							
			if (s == null)
			{
				s = sw;
			}
		}
					
		s.retained = true;
		
		foreach (var t in s.Terminals)
		{
			if (t.ConnectivityNode == cn)
			{
				continue;
			}
			
			if (TopologyProcessor.FindTopoNodeZW(t.ConnectivityNode) == null)
			{
				AssignBusNameMarker(t, NextNumber, 1, "AssignRetainedRCE");
			}
		}
	}
	
	private void AssignSwitchRetained(Switch branch)
	{
		var nums = TopologyProcessor.GetBranchTopoNumbers(branch);
		
		if (nums.HasValue == false)
		{
			branch.retained = false;
			return;
		}
	
		if (nums.Value.Item1 == nums.Value.Item2
			|| AssignedRetainedBranches.Contains(nums.Value))
		{
			branch.retained = false;
			return;
		}
		
		branch.retained = true;
		AssignedRetainedBranches.Add(nums.Value);
	}	
	
	private IEnumerable<T> GetChildrenOfType<T>(IMalObject entity, bool recursive)
	{
		var stack = new Stack<IMalObject>();
		
		entity.GetChildren().ToList().ForEach(c => stack.Push(c));
		while (stack.Count != 0)
		{
			var child = stack.Pop();
			
			if (recursive == true)
			{
				child.GetChildren().ToList().ForEach(c => stack.Push(c));
			}
			
			if (child is T)
			{
				yield return (T)child;
			}
		}
	}
}

//--------------------------------------------------------------------------------------------------------

static public class TopologyProcessor
{
	private static Guid AssignedUid = Guid.Parse("10000D96-0000-0000-C000-0000006D746C");
      	private volatile static IExtendedDataSourceService DataSource = 
      		ServiceLocator.Current.GetInstance<IExtendedDataSourceService>();	
              
	public static ConnectivityNode FindTopoNodeZW(ConnectivityNode start)
	{
		ConnectivityNode result = null;
			
		CimBFS(start,
			(terminal) => 
			{
				if (terminal.BusNameMarker != null)
				{
					result = terminal.ConnectivityNode;
					return true;
				}
				return false;
			},
			MoveNextDelegateZW
		);
		
		return result;
	}   
	
	public static T FindTopoOfTypeZW<T>(ConnectivityNode start)
	{
		T result = default(T);
			
		CimBFS(start,
			(terminal) => 
			{
				if (terminal.ConductingEquipment is T ce)
				{
					result = ce;
					return true;
				}
				return false;
			},
			MoveNextDelegateZW
		);
		
		return result;
	}
	
	public static bool MoveNextDelegateZW(ConductingEquipment ce)
	{
		var sw = ce as Switch;
		if (sw == null)
		{
			return false;
		}
				
		bool open = sw.normalOpen;
			
	     	var container = DataSource.SvDA_Manager.GetContainer(AssignedUid, nameof(SvSwitchClosed)) as IContainer<Key>;
		if (container != null)
		{
			var stateField = container.Fields[nameof(SvSwitchClosed.state)].Index;
			if (container.TryRead(sw.Uid, stateField, out int state))
			{
				open = state == 0 ? true : false;
			}
		}
							
		if (sw.retained == false && open == false)
		{
			return true;
		}
				
		return false;
	}

	public static void CimBFS(ConnectivityNode start, 
		Func<Terminal, bool> StopDelegate,
		Func<ConductingEquipment, bool> MoveNextDelegate)
	{
		if (start == null)
		{
			return;
		}
	
		var visited = new HashSet<Terminal>();
		var stack = new Queue<Terminal>();
		start.Terminals.ToList().ForEach(t => stack.Enqueue(t));
		
		while (stack.Count > 0)
		{
			var next = stack.Dequeue();
			
			if (visited.Contains(next))
			{
				continue;
			}
				
			visited.Add(next);
			
			if (StopDelegate(next) == true)
			{
				return;
			}
			
			if (MoveNextDelegate(next.ConductingEquipment) == true)
			{
				var pt = next.ConductingEquipment as PowerTransformer;
				if (pt != null)
				{
					foreach (var p in pt.PowerTransformerEnd)
					{				
						p.Terminal.ConnectivityNode.Terminals
							.ToList().ForEach(t => stack.Enqueue(t));
					}
				}
				else
				{
					next.ConductingEquipment.Terminals.ToList().ForEach(
						t => 
						{ 
							if (t != next) 
							{
								t.ConnectivityNode.Terminals
									.ToList().ForEach(t2 => stack.Enqueue(t2));
							}
						});			
				}
			}	
		}	
	}	
	
	public static IEnumerable<Switch> FindSwitchAround(ConnectivityNode node)
	{
		var result = new List<Switch>();
	
		CimBFS(node,
			(terminal) => 
			{
				if (terminal.ConductingEquipment is Switch sw && 
					!(terminal.ConductingEquipment is GroundDisconnector))
				{
					result.Add(sw);
					return true;
				}
				return false;
			},
			MoveNextDelegateZW
		);	
		
		return result;
	}
	
	public static (int, int)? GetBranchTopoNumbers(ConductingEquipment branch)
	{
		if (branch.Terminals.Count() != 2)
		{
			return null;	
		}
		
		var nums = new List<int>();
		foreach (var terminal in branch.Terminals)
		{
			CimBFS(terminal.ConnectivityNode,
				t => 
				{	
					var bnms = t.ConnectivityNode.Terminals
						.Where(t => t.BusNameMarker != null)
						.Select(t => t.BusNameMarker)
						.OrderBy(m => m.priority);
						
					if (bnms.Count() != 0)
					{
						nums.Add(bnms.First().number);
						return true;
					}
				
					return false;
				},
				MoveNextDelegateZW
			);
		}
		
		if (nums.Count() == 0 || nums.Count() > 2)
		{
			return null;
		}
		
		nums.Sort();
			
		return (nums.First(), nums.Last());
	}	
}

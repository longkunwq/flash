using Caliburn.Micro;
using Gemini.Framework;
using Kunyi.DAS2.XCP.Entity.Utility;
using Kunyi.VCarDAS2.Simulation.Entity.Entitys;
using Kunyi.VCarDAS2.Simulation.Entity.Entitys.MCModel;
using Kunyi.VCarDAS2.Simulation.Entity.Entitys.XCPModel.BasicConfig.TransportLayer;
using Kunyi.VCarDAS2.Simulation.Entity.Entitys.XCPModel.BasicConfig;
using Kunyi.VCarDAS2.Simulation.Entity.Entitys.XCPModel.Enums;
using Kunyi.VCarDAS2.Simulation.Entity.Entitys.XCPModel.Measurement;
using Kunyi.VCarDAS2.Simulation.View.ViewModels.XCPModel.Helpers;
using Kunyi.VCarDAS2.Simulation.View.Views.MCModel;
using KunYi.A2l;
using KunYi.A2l.Entity;
using SharpDX;
using Syncfusion.Linq;
using Syncfusion.UI.Xaml.Grid;
using Syncfusion.UI.Xaml.TreeGrid;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Input;
using static KunYi.A2l.Entity.Asap2IFDataXCP;
using Resources = Kunyi.VCarDAS2.Simulation.Entity.Properties.XCPModelResources;
using Syncfusion.Windows.Shared;
using System.Threading.Tasks;
using System.Windows;
using Kunyi.DAS2.Common.Interface.Device;
using Kunyi.DAS2.Common.Interface.MeasurementData;
using Kunyi.VCarDAS2.Simulation.Entity.Entitys.XCPModel.Interface;
using Microsoft.Win32;
using System.Xml.Serialization;
using System.Xml.Linq;
using Newtonsoft.Json;
using Kunyi.VCarDAS2.Simulation.Entity.Properties;
using Kunyi.DAS2.Common.Interface.Simulation;
using Kunyi.DAS2.Common.Interface.Project;

namespace Kunyi.VCarDAS2.Simulation.View.ViewModels.MCModel
{
    [Export(typeof(MeasurementConfigViewModel))]
    public class MeasurementConfigViewModel : Screen
    {
        //测量量配置
        ConfigInfo _config;

        bool _isBusy;
        bool _nameChecked;
        bool _typeChecked;
        List<MeasurementItemInfo> _allMeasurements;
        ObservableCollection<MeasurementItemInfo> _measurements;
        BindingList<MenuItemInfo> _daqMenuItems;
        BindingList<DAQEventNode> _daqEventNodes;
        ObservableCollection<PollingEventNode> _pollingNodes;
        IEventAggregator eventAggregator;
        string _daqUsedPercent;
        string _entryPercent;

        string _measurementFilterText;

        SfDataGrid _measurementsSfGrid { get; set; }
        SfTreeGrid _pollingTreeGrid { get; set; }
        SfTreeGrid _daqTreeGrid { get; set; }

        Dictionary<string, List<ODTInfo>> DaqDictionary { get; set; } = new Dictionary<string, List<ODTInfo>>();

        Asap2IFDataCCP _ccp;
        Asap2IFDataXCP.TransportLayerBase _xcp;
        Asap2Module _module;
        const string BuildSuccessMsg = "Build_O_D_T_Success";

        Node _node;

        public bool IsBusy
        {
            get => _isBusy;
            set => Set(ref _isBusy, value);
        }

        public bool NameChecked
        {
            get => _nameChecked;
            set => Set(ref _nameChecked, value);
        }

        public bool TypeChecked
        {
            get => _typeChecked;
            set => Set(ref _typeChecked, value);
        }
        /// <summary>
        /// 
        /// </summary>
        public int MaxOdtLength { get; set; } = 252;
        /// <summary>
        /// 所有的测量量
        /// </summary>
        public ObservableCollection<MeasurementItemInfo> Measurements
        {
            get => _measurements;
            set => Set(ref _measurements, value);
        }

        public BindingList<MenuItemInfo> DaqMenuItems 
        { 
            get => _daqMenuItems;
            set => Set(ref _daqMenuItems, value); 
        }

        public BindingList<DAQEventNode> DaqEventNodes
        {
            get => _daqEventNodes;
            set => Set(ref _daqEventNodes, value);
        }

        public ObservableCollection<PollingEventNode> PollingNodes
        {
            get => _pollingNodes;
            set => Set(ref _pollingNodes, value);
        }

        public string SizeUsedPercent
        {
            get => _daqUsedPercent;
            set => Set(ref _daqUsedPercent, value);
        }

        public string EntryUsedPercent
        {
            get => _entryPercent;
            set => Set(ref _entryPercent, value);
        }

        public string MeasurementFilterText
        {
            get => _measurementFilterText;
            set => Set(ref _measurementFilterText, value);
        }

        public ICommand AddToPollingCommand100 { get; set; }
        public ICommand AddToPollingCommand500 { get; set; }
        public ICommand AddToPollingCommand1000 { get; set; }

        public ICommand RemovePollingCommand { get; set; }

        public ICommand RemoveDAQCommand { get; set; }

        public MeasurementConfigViewModel()
        {
            AddToPollingCommand100 = new RelayCommand(AddToPolling100, HasItems);
            AddToPollingCommand500 = new RelayCommand(AddToPolling500, HasItems);
            AddToPollingCommand1000 = new RelayCommand(AddToPolling1000, HasItems);

            RemovePollingCommand = new RelayCommand(RemovePolling, CanRemovePollingExecute);
            RemoveDAQCommand = new RelayCommand(RemoveDaq, HasItems);

            _measurements = new ObservableCollection<MeasurementItemInfo>();
            _daqMenuItems = new BindingList<MenuItemInfo>();
            _daqEventNodes = new BindingList<DAQEventNode>();
            _pollingNodes = new ObservableCollection<PollingEventNode>();
            eventAggregator=IoC.Get<IEventAggregator>();
        }


        public void ShowConfig(Node node)
        {
            _node = node;
            _measurements.Clear();
            _daqMenuItems.Clear();
            _daqEventNodes.Clear();
            _pollingNodes.Clear();
            DaqDictionary.Clear();
            SizeUsedPercent = string.Empty;
            EntryUsedPercent = string.Empty;
            NameChecked = false;
            TypeChecked = false;
            MeasurementFilterText = string.Empty;

            var measurements = new List<MeasurementItemInfo>();
            var daqs = new List<DAQEventNode>();

            IsBusy = true;

            //当未读取到配置信息，说明当前节点未配置传输层，需要先配置节点的传输层信息
            var config = SelectViewModel.ReadConfig(node.Name);
            if (config == null)
            {
                Gemini.Modules.Dialogs.MessageBox.ShowMessageForDAS(Resources.NeedConfigTransportLayerFirst, Resources.Failed, Gemini.Modules.Dialogs.DasMessageType.Error);
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    _config = config;
                    string path = config.A2lFile;
                    if ((!string.IsNullOrEmpty(path))&&(!File.Exists(config.A2lFile)))
                    {
                        string path1 = IoC.Get<IProject>().GetModuleRootPath("Simulation");
                        path = Path.Combine(path1, _node.Id.Replace("-","").Trim(), "a2l", config.A2lFile);
                    }
                    if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    {
                        Gemini.Modules.Dialogs.MessageBox.ShowMessageForDAS(Resources.NullA2LTip, Resources.Failed, Gemini.Modules.Dialogs.DasMessageType.Error);
                        return;
                    }

                    A2lParser parser = new A2lParser();
                    var result = parser.Parser(path);
                    if (!result || parser.Project is null || parser.Project.Modules is null || !parser.Project.Modules.Any())
                    {
                        Gemini.Modules.Dialogs.MessageBox.ShowMessageForDAS(string.Format(Resources.A2LParseFailed, path), Resources.Failed, Gemini.Modules.Dialogs.DasMessageType.Error);
                        return;
                    }

                    var module = parser.Project?.Modules?.FirstOrDefault(x => x.Name == config.ModelName);
                    if (module == null)
                    {
                        Gemini.Modules.Dialogs.MessageBox.ShowMessageForDAS(string.Format(Resources.A2lModuleNotExit, config.ModelName), Resources.Failed, Gemini.Modules.Dialogs.DasMessageType.Error);
                        return;
                    }

                    _module = module;

                    measurements = SelectViewModel.GetAllMeasurements(module);
                    _allMeasurements = measurements.ToList();

                    object transportLayerInstace = null;
                    if (config.TransportLayerType == MCModelType.CCP)
                    {
                        var ccps = module.IFDataObject.Values?.OfType<Asap2IFDataCCP>()?.ToList();
                        if (config.TranportLayerIndex < ccps.Count)
                        {
                            transportLayerInstace = ccps[config.TranportLayerIndex];
                            _ccp = ccps[config.TranportLayerIndex];
                        }
                        else if(ccps.Any())
                        {
                            transportLayerInstace = ccps[0];
                            _ccp = ccps[0];
                        }
                    }
                    else
                    {
                        var xcps = module.IFDataObject?.Values?.OfType<Asap2IFDataXCP>()?.ToList();
                        List<TransportLayerBase> trans = new List<TransportLayerBase>();
                        if (xcps?.Any()==true)
                        {
                        xcps.ForEach(xcp =>
                        {
                            trans.AddRange(xcp?.TransportLayers);
                        });
                        if (config.TransportLayer.Equals("XCP_ON_CAN"))
                        {
                            var cans = trans.OfType<Asap2IFDataXCP.XCPOnCan>().ToList();
                            if (config.TranportLayerIndex < cans.Count)
                                _xcp = cans[config.TranportLayerIndex];
                            else
                                _xcp = cans.FirstOrDefault();
                        }
                        else if (config.TransportLayer.Equals("XCP_UDP_IP"))
                        {
                            var eths = trans.OfType<Asap2IFDataXCP.XCPOnUdpIp>().ToList();
                            if (config.TranportLayerIndex < eths.Count)
                                _xcp = eths[config.TranportLayerIndex];
                            else
                                _xcp = eths.FirstOrDefault();
                        }
                        else if (config.TransportLayer.Equals("XCP_TCP_IP"))
                        {
                            var eths = trans.OfType<Asap2IFDataXCP.XCPOnTcpIp>().ToList();
                            if (config.TranportLayerIndex < eths.Count)
                                _xcp = eths[config.TranportLayerIndex];
                            else
                                _xcp = eths.FirstOrDefault();
                        }
                        else if (config.TransportLayer.Equals("XCP_ON_TCP"))
                        {
                            var tcps = trans.OfType<Asap2IFDataXCP.XCPOnTcpIp>().ToList();
                            if (config.TranportLayerIndex < tcps.Count)
                                _xcp = tcps[config.TranportLayerIndex];
                            else
                                _xcp = tcps.FirstOrDefault();
                        }
                        else if (config.TransportLayer.Equals("XCP_ON_UDP"))
                        {
                            var udps = trans.OfType<Asap2IFDataXCP.XCPOnTcpIp>().ToList();
                            if (config.TranportLayerIndex < udps.Count)
                                _xcp = udps[config.TranportLayerIndex];
                            else
                                _xcp = udps.FirstOrDefault();
                        }
                        else
                        {
                            var names = trans.Where(x => x.TransportLayerInstance != null && x.TransportLayerInstance.Equals(config.TransportLayer)).ToList();
                            if (config.TranportLayerIndex < names.Count)
                                _xcp = trans.Where(x => x.TransportLayerInstance.Equals(config.TransportLayer)).ToList()[config.TranportLayerIndex];
                            else
                                _xcp = names.FirstOrDefault();
                        }
                        transportLayerInstace = _xcp;
                        }
                    }

                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        daqs = InitDAQEvent(module, config.TransportLayerType, transportLayerInstace);
                        if (daqs?.Any() ?? false)
                        {
                            DaqEventNodes = new BindingList<DAQEventNode>(daqs);
                        }
                        Measurements = new ObservableCollection<MeasurementItemInfo>(measurements);
                        ///Polling树节点
                        PollingNodes = new ObservableCollection<PollingEventNode>()
                        {
                            new PollingEventNode
                            {
                                Cycle = 100,
                                Name = "Polling 100ms",
                                Children = new ObservableCollection<PollingEventNode>(),
                            },
                            new PollingEventNode
                            {
                                Cycle = 500,
                                Name = "Polling 500ms",
                                Children = new ObservableCollection<PollingEventNode>(),
                            },
                            new PollingEventNode
                            {
                                Cycle = 1000,
                                Name = "Polling 1000ms",
                                Children = new ObservableCollection<PollingEventNode>(),
                            },
                        };

                        if (config.SelectedDAQSignals.Any())
                        {
                            foreach (var daqKeyPair in config.SelectedDAQSignals)
                            {
                                if (daqKeyPair.Value.Any())
                                {
                                    List<MeasurementItemInfo> selected = new List<MeasurementItemInfo>();
                                    daqKeyPair.Value.ForEach(x =>
                                    {
                                        var m = measurements.FirstOrDefault(y => y.NAME.Equals(x.NAME));
                                        if (m != null)
                                            selected.Add(m);
                                    });

                                    var eventNode = DaqEventNodes.FirstOrDefault(x => x.EVENT_CHANNEL_NAME.Equals(daqKeyPair.Key));
                                    if (eventNode != null && selected.Any())
                                    {
                                        var result = BuildOdtEntryNew(selected, daqKeyPair.Key, true);
                                        ActionAfterBuildODTItem(result, eventNode, selected);
                                        RefreshSourceGrid();
                                        DAQChanged();
                                    }
                                }
                            }
                        }

                        if (config.SelectedPollingDic.Any())
                        {
                            foreach (var pollingKeyPair in config.SelectedPollingDic)
                            {
                                var signals = pollingKeyPair.Value;
                                AddPolling(signals, pollingKeyPair.Key);
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    CommonHelper.LogException(ex);
                }
                finally
                {
                    IsBusy = false;
                }
            });

            IoC.Get<IWindowManager>().ShowDialogAsync(this);
        }

        public static bool CanShowConfig(Node node)
        {
            if (node is null)
                return false;

            var extendFilePath = Path.Combine(IoC.Get<IXCPInternalService>().GetXCPRootPath(), "Models", node.Name, "Imports", "extentFile");
            if (!Directory.Exists(extendFilePath)) return false;
            var configPath = Path.Combine(extendFilePath, "config.json");

            var jsonFilePath = Path.Combine(IoC.Get<IXCPInternalService>().GetXCPRootPath(), "Models", $"{node.Name}.json");

            if (!File.Exists(configPath) || !File.Exists(jsonFilePath))
                return false;

            return true;
        }

        public async void Confirm()
        {
            try
            {
                IsBusy = true;
                _config.SelectedDAQSignals.Clear();
                _config.SelectedPollingDic.Clear();

                await ConfigMeasurement();

                ReWriteToConfig();
               await eventAggregator.PublishOnBackgroundThreadAsync(new XCPSignalChangedMessage());

            }
            catch (Exception ex)
            {
                CommonHelper.ShowError(ex.Message);
            }
            finally
            {
                IsBusy = false;
                await TryCloseAsync();
            }
        }

        public async void ImportConfiguration()
        {
            try
            {
                OpenFileDialog dialog = new OpenFileDialog();
                dialog.Filter = "xcpgrp文件(*.xcpgrp);json文件(*.json)|*.xcpgrp;*.json";
                if(dialog.ShowDialog() == true)
                {
                    List<Tuple<string, uint>> pollings = new List<Tuple<string, uint>>();   //Polling信号
                    Dictionary<string, List<string>> daqs = new Dictionary<string, List<string>>();     //DAQ信号

                    IsBusy = true;

                    //加载CANoe的.xcpgrp格式配置文件
                    if (Path.GetExtension(dialog.FileName).ToLower().Equals(".xcpgrp"))
                    {
                        await Task.Run(() =>
                        {
                            LoadMeasurementConfigurationFromXcpGroup(dialog.FileName, pollings, daqs);
                        });
                    }//加载DAS2的自定义测量量配置文件
                    else
                    {
                        await Task.Run(() =>
                        {
                            LoadMeasurementConfigurationFromJson(dialog.FileName, pollings, daqs);
                        });
                    }

                    ApplayMeasurementConfigration(pollings, daqs);

                    IsBusy = false;
                }

                
            }catch(Exception ex)
            {

            }
            finally
            {

            }
        }

        public void ExportConfiguration()
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "json(*.json)|*.json";
            if(dialog.ShowDialog() == true)
            {
                MeasurementConfiguration configuration = new MeasurementConfiguration()
                {
                    Signals = new List<MeasurementSignal>(),
                };

                if (DaqDictionary.Any())
                {
                    DaqDictionary.ForEach(daqkeypair =>
                    {
                        daqkeypair.Value.ForEach(odtEntry =>
                        {
                            odtEntry.ODT_ENTRY.ForEach(odtEntryItem =>
                            {
                                configuration.Signals.Add(new MeasurementSignal()
                                {
                                    Name = odtEntryItem.MEASUREMENT_NAME,
                                    EventName = daqkeypair.Key,
                                });
                            });
                        });
                    });
                }

                if (PollingNodes.Any())
                {
                    foreach(var polling in PollingNodes)
                    {
                        foreach(var signal in polling.Children)
                        {
                            configuration.Signals.Add(new MeasurementSignal()
                            {
                                Name = signal.Name,
                                Cycle = (uint)polling.Cycle,
                            });
                        }
                    }
                }

                string lines = JsonConvert.SerializeObject(configuration);

                File.WriteAllText(dialog.FileName, lines);
            }
        }

        public void FilterSearchHandle(object sender, object args)
        {
            try
            {
                if (sender is FrameworkElement element)
                {
                    if (element.Tag is SfDataGrid dataGrid) RefreshFilter(dataGrid);
                }
            }
            catch (Exception ex)
            {
                // CommonHelper.LogException(ex);
            }
        }

        public bool FilerRecords(object obj)
        {
            try
            {
                if (obj is MeasurementItemInfo calItem)
                {
                    if (MeasurementFilterText.IsNullOrWhiteSpace()) return true;

                    var columns = new List<string>();
                    if (NameChecked) columns.Add("NAME");
                    if (TypeChecked) columns.Add("DATATYPE");
                    var matchResult = GetTargetPropertiesValues<MeasurementItemInfo>(calItem, columns).Any(value =>
                    {
                        return MatchText(value, MeasurementFilterText);
                    });
                    return matchResult;
                }
            }
            catch (Exception ex)
            {
                CommonHelper.LogException(ex);
                return false;
            }
            return true;
        }

        List<string> GetTargetPropertiesValues<T>(T item, List<string> proNames)
        {
            try
            {
                var resultList = new List<string>();
                proNames?.ForEach(name =>
                {
                    var proValue = item.GetType().GetProperty(name)?.GetValue(item)?.ToString();

                    resultList.Add(proValue);

                });

                return resultList;
            }
            catch (Exception)
            {
                return null;
            }
        }

        bool MatchText(string sourceText, string filterText)
        {
            try
            {
                if (filterText.IsNullOrWhiteSpace())
                {
                    return true;
                }
                if (!filterText.IsNullOrWhiteSpace() && sourceText.IsNullOrWhiteSpace())
                {
                    return false;
                }
                return sourceText.Contains(filterText, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                CommonHelper.LogException(ex);
                return false;
            }
        }

        protected override void OnViewLoaded(object view)
        {
            base.OnViewLoaded(view);

            if(view is MeasurementConfigView mcv)
            {
                _measurementsSfGrid = mcv.MSourceGrid;
                _pollingTreeGrid = mcv.PollingGrid;
                _daqTreeGrid = mcv.DAQGrid;
            }
        }

        void AddToPolling100(object sender)
        {
            try
            {
                AddPolling(sender, 100);
            }catch(Exception ex)
            {
                CommonHelper.LogException(ex);
            }
        }

        void AddToPolling500(object sender)
        {
            try
            {
                AddPolling(sender, 500);
            }
            catch (Exception ex)
            {
                CommonHelper.LogException(ex);
            }
        }

        void AddToPolling1000(object sender)
        {
            try
            {
                AddPolling(sender, 1000);
            }
            catch (Exception ex)
            {
                CommonHelper.LogException(ex);
            }
        }

        bool CanRemovePollingExecute(object obj)
        {
            if (obj is IList list)
            {
                return list.OfType<PollingEventNode>()?.Any() ?? false;
            }
            return false;
        }

        void RemovePolling(object sender)
        {
            try
            {
                if (sender is IList list)
                {
                    var rootNodes = PollingNodes.ToList();

                    var allNodes = list.OfType<PollingEventNode>().ToList();
                    //if (_pollingTreeGrid != null) _pollingTreeGrid.ItemsSource = null;

                    var selectedRootNodes = allNodes.Where(n => n.ParentCycle == -1).ToList();
                    //源测量信号集合
                    var source = _measurements.ToList();

                    //移除根节点全选的
                    selectedRootNodes?.ForEach(rt =>
                    {
                        allNodes.RemoveAll(n => rt.Children.Contains(n));
                        source.AddRange(rt.Children.Select(n => n.MeasurementItem));
                        rt.Children.Clear();
                        allNodes.Remove(rt);
                    });


                    //移出非根节点
                    int rootIndex = 0;
                    Dictionary<int, List<PollingEventNode>> nodeChildrenDic = new Dictionary<int, List<PollingEventNode>>();
                    rootNodes.ForEach(rootItem =>
                    {
                        var nodeChildren = rootItem.Children.ToList();
                        int nowCycle = rootItem.Cycle;
                        var itemRemove = allNodes.Where(p => p.Cycle == nowCycle).ToList();

                        nodeChildren = nodeChildren.Except(itemRemove).ToList();
                        allNodes = allNodes.Except(itemRemove).ToList();
                        source.AddRange(itemRemove.Select(p => p.MeasurementItem).ToList());

                        nodeChildrenDic.Add(rootIndex, nodeChildren);
                        rootIndex += 1;
                    });
                    for (int i = 0; i < rootNodes.Count; i++)
                    {
                        rootNodes[i].Children = new ObservableCollection<PollingEventNode>(nodeChildrenDic[i]);
                    }

                    //PollingNodes = new ObservableCollection<PollingEventNode>(rootNodes);//重新绑定
                    //if (_pollingTreeGrid != null) _pollingTreeGrid.ItemsSource = PollingNodes;

                    var sortList = source.OrderBy(signal => signal.NAME);
                    Measurements = new ObservableCollection<MeasurementItemInfo>(sortList);
                    RefreshSourceGrid();
                    RefreshFilter(_measurementsSfGrid);
                    NotifyOfPropertyChange(nameof(PollingNodes));
                }
            }
            catch (Exception ex)
            {
                CommonHelper.LogException(ex);
            }
            finally
            {
                //CommonHelper.GlobalLoading?.Close();
            }
        }

        void RemoveDaq(object sender)
        {
            if (sender is IList list)
            {
                var rootNodes = DaqEventNodes;
                var rootNodeChildrens = list.OfType<DAQEventNode>().Where(n => n.NodeLevel == 0).SelectMany(n => n.Children).ToList();
                var childNodes = list.OfType<DAQEventNode>().Where(n => n.NodeLevel > 0).ToList();
                childNodes.AddRange(rootNodeChildrens);
                childNodes = childNodes.Distinct().ToList();
                var measurements = childNodes.Select(n => n.MeasurementItem).ToList();

                ////从DAQ根节点移除
                //childNodes.ForEach(n =>
                //{
                //    var rootNode = rootNodes.FirstOrDefault(nd => nd.EventName.Equals(n.EventName));
                //    if (rootNode != null && rootNode.Children.Contains(n))
                //    {
                //        rootNode.Children.Remove(n);
                //    }
                //});
                rootNodes.ForEach(n =>
                {
                    var itemChild = n.Children.ToList();
                    for (int i = childNodes.Count - 1; i > -1; i--)
                    {
                        if (childNodes[i].EventName != n.EventName) continue;
                        itemChild.Remove(childNodes[i]);
                        childNodes.RemoveAt(i);
                    }
                    n.Children = new ObservableCollection<DAQEventNode>(itemChild);
                    //xcpService.BuildDynamicOdtEntry(n.Children.Select(c => c.MeasurementItem).ToList(), n.EVENT_CHANNEL_SHORT_NAME);
                    BuildOdtEntryNew(n.Children.Select(c => c.MeasurementItem).ToList(), n.EVENT_CHANNEL_NAME, false);
                });
                DAQChanged();

                //重新绑定，刷新下拉箭头
                if (_daqTreeGrid != null)
                {
                    _daqTreeGrid.ItemsSource = null;
                    _daqTreeGrid.ItemsSource = DaqEventNodes;
                }

                //添加到源测量信号集合
                var source = _measurements.ToList();
                source.AddRange(measurements);
                var sortList = source.OrderBy(signal => signal.FUNCTION_NAME).ThenBy(signal => signal.NAME);
                Measurements = new ObservableCollection<MeasurementItemInfo>(sortList);
                RefreshSourceGrid();
                RefreshFilter(_measurementsSfGrid);
            }
        }


        void AddPolling(object obj, int pollingTime)
        {
            var pollings = PollingNodes.ToList();

            var polling = pollings.FirstOrDefault(n => n.Cycle == pollingTime);

            if (obj is IList list)
            {
                var sourceList = _measurements.ToList();

                var filterList = list.OfType<MeasurementItemInfo>()?.Distinct(new MeasurementCompare())?.ToList();
                var existsNames = PollingNodes.SelectMany(m => m.Children)?.Select(m => m.MeasurementItem.NAME)?.ToList();
                filterList.RemoveAll(f => existsNames?.Contains(f.NAME) ?? false);
                var childList = polling.Children.ToList();
                if (childList == null) childList = new List<PollingEventNode>();
                filterList?.ForEach(item =>
                {
                    sourceList.Remove(sourceList.FirstOrDefault(i => i.NAME == item.NAME));
                    childList.Add(new PollingEventNode()
                    {
                        Cycle = pollingTime,
                        Name = item.NAME,
                        ParentCycle = pollingTime,
                        MeasurementItem = item,
                    });
                    //rootNode.Children.Add(BuildChildrenNodeByCycle(eventCycle, item));
                });
                polling.Children = new ObservableCollection<PollingEventNode>(childList);

                PollingNodes = new ObservableCollection<PollingEventNode>(pollings);

                Measurements = new ObservableCollection<MeasurementItemInfo>(sourceList);
                RefreshSourceGrid();
                RefreshFilter(_measurementsSfGrid);
                NotifyOfPropertyChange(nameof(PollingNodes));
            }
        }

        bool HasItems(object obj)
        {
            if (obj is IList list)
            {
                return list.Count > 0;
            }
            return false;
        }

        void AddItemsToDaq(DAQEventNode node)
        {
            var existItems = node.Children.Select(n => n.MeasurementItem).ToList();
            var allExitItems = DaqEventNodes.SelectMany(d => d.Children).Select(c => c.MeasurementItem).ToList();

            var addItems = _measurementsSfGrid?.SelectedItems?.OfType<MeasurementItemInfo>()?.Distinct(new MeasurementCompare());

            //找出地址为0的项来
            var zeroAddressItems = addItems.Where(p => (p.ECU_ADDRESS ?? 0) == 0).ToList();
            if (zeroAddressItems != null && zeroAddressItems.Any())
            {
                addItems = addItems.Except(zeroAddressItems).ToList();
                CommonHelper.ShowWarn(string.Format(Resources.ZeroCannotAddDAQ, zeroAddressItems.Count));
            }

            var filterItems = addItems.Where(item => !allExitItems.Select(exItem => exItem.NAME).Contains(item.NAME)).ToList();
            var repeatItems = addItems.Where(item => allExitItems.Select(exItem => exItem.NAME).Contains(item.NAME)).Select(item => item.NAME).ToList();
            if (repeatItems.Any())
            {
                if (repeatItems.Any())
                {
                    var errStr = string.Join(", ", repeatItems);
                    CommonHelper.ShowWarn($"{Resources.RepeatTip}\n{errStr}");
                }
            }
            existItems.AddRange(filterItems);
            //var result = BuildDynamicOdtEntry(existItems, rootNode.EVENT_CHANNEL_SHORT_NAME);
            var result = BuildOdtEntryNew(existItems, node.EVENT_CHANNEL_NAME, true);

            //构建完DAQ之后的操作
            ActionAfterBuildODTItem(result, node, filterItems, true);

            RefreshSourceGrid();

            RefreshFilter(_measurementsSfGrid);
            DAQChanged();
        }

        public void ActionAfterBuildODTItem(string result, DAQEventNode rootNode, List<MeasurementItemInfo> filterItems, bool showTip = true)
        {
            if (string.IsNullOrEmpty(result))
                return;
            else if (result.Equals(BuildSuccessMsg))
            {
                var nodeChildren = rootNode.Children.ToList();
                filterItems.ForEach(i =>
                {
                    _measurements.Remove(i);
                    rootNode.Children.Add(new DAQEventNode
                    {
                        NodeLevel = 1,
                        EVENT_CHANNEL_NAME = i.NAME,
                        MeasurementItem = i,
                        EventName = rootNode.EventName
                    });
                    rootNode.Children.OrderBy(c => c.MeasurementItem.NAME);
                });
                //重新绑定，刷新下拉箭头
                if (_daqTreeGrid != null)
                {
                    _daqTreeGrid.ItemsSource = null;
                    _daqTreeGrid.ItemsSource = DaqEventNodes;
                }
            }
            else
            {
                var errorItem = filterItems.FirstOrDefault(m => m.NAME.Equals(result));
                if (errorItem != null)
                {
                    var nodeChildren = rootNode.Children.ToList();

                    var index = filterItems.IndexOf(errorItem);
                    filterItems.Take(index).ForEach(i =>
                    {
                        _measurements.Remove(i);
                        rootNode.Children.Add(new DAQEventNode
                        {
                            NodeLevel = 1,
                            EVENT_CHANNEL_NAME = i.NAME,
                            MeasurementItem = i,
                            EventName = rootNode.EventName
                        });
                        rootNode.Children.OrderBy(c => c.MeasurementItem.NAME);
                    });

                    //重新绑定，刷新下拉箭头
                    if (_daqTreeGrid != null)
                    {
                        _daqTreeGrid.ItemsSource = null;
                        _daqTreeGrid.ItemsSource = DaqEventNodes;
                    }
                }
                if (showTip)
                {
                    string mes = "";
                    int count = 0;
                    for (int i = filterItems.IndexOf(errorItem); i < filterItems.Count; i++)
                    {
                        mes += $"{filterItems[i].NAME}\r\n";
                        count++;
                        if (count > 5)
                        {
                            mes += "...";
                            break;
                        }
                    }
                    CommonHelper.ShowWarn($"{Resources.OverLoadTip}\r\n" + mes);
                }
            }
        }

        public void RefreshSourceGrid()
        {
            var sortList = _measurements.ToList();
            sortList.OrderBy(s => s.FUNCTION_NAME)
                .ThenBy(s => s.NAME);
            Measurements = new ObservableCollection<MeasurementItemInfo>(sortList);
        }

        public void RefreshFilter(SfDataGrid grid)
        {
            if (grid != null)
            {
                //FilterText = SourceFilterText;
                if (_measurementsSfGrid.View != null && _measurementsSfGrid.View?.Filter == null) _measurementsSfGrid.View.Filter = FilerRecords;
                _measurementsSfGrid.View.RefreshFilter();
            }
        }

        public void DAQChanged()
        {
            SizeUsedPercent = GetDAQUsedSizePercent(out string entryPercent);
            EntryUsedPercent = entryPercent;
        }

        /// <summary>
        /// 构建XCP或CCP的ODT列表
        /// </summary>
        /// <param name="measurementInfos"></param>
        /// <param name="currentDaqName"></param>
        /// <param name="isAdd"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        string BuildOdtEntryNew(List<MeasurementItemInfo> measurementInfos, string currentDaqName, bool isAdd)
        {
            switch (_config.TransportLayerType)
            {
                case MCModelType.XCP_ON_CAN:
                case MCModelType.XCP_ON_UDP_IP:
                case MCModelType.XCP_ON_TCP_IP:
                    return BuildXCPOdtEntryNew(measurementInfos, currentDaqName, isAdd);
                case MCModelType.CCP:
                    return BuildCCPOdtEntryNew(measurementInfos, currentDaqName, isAdd);
                default:
                    throw new NotImplementedException();
            }
        }

        string BuildXCPOdtEntryNew(List<MeasurementItemInfo> measurementInfos, string currentDaqName, bool isAdd)
        {
            try
            {
                //判断是否是CANFD
                Asap2IFDataXCP.CanFd canfd = null;
                if (_xcp is Asap2IFDataXCP.XCPOnCan xcpCanInfo)
                {
                    canfd = xcpCanInfo?.CanFd;
                }

                bool isSupportTimeStamp = false;
                byte timeStampSize = 0;
                var daq = _xcp?.CommonParameter?.Daq ?? _module?.IFDataObject?.Values?.OfType<Asap2IFDataXCP>()?.FirstOrDefault()?.CommonParameter.Daq;
                if (daq.TimeStampSupported != null)
                {
                    timeStampSize = Convert.ToByte(daq.TimeStampSupported.TimeStampSize);
                    if (timeStampSize > 0) isSupportTimeStamp = true;
                }

                //基本信息
                var node = DaqEventNodes.FirstOrDefault(n => n.EVENT_CHANNEL_NAME == currentDaqName);
                int odtCount = 0;
                int entryCount = 0;
                var commonParam = _xcp?.CommonParameter ?? _module?.IFDataObject?.Values?.OfType<Asap2IFDataXCP>()?.FirstOrDefault()?.CommonParameter;
                var daqMemroy = commonParam?.Daq?.DaqMemroyConsumption;
                var totalSize = (int)(daqMemroy?.DaqMemroyLimit ?? int.MaxValue);
                var perDaqSize = (int)(daqMemroy?.DaqSize ?? 0);
                var perOdtSize = (int)(daqMemroy?.OdtSize ?? 0);
                ushort perOdtEntrySize = (ushort)((daqMemroy?.OdtEntrySize ?? 0) + (daqMemroy?.OdtDaqBufferElementSize ?? 0));

                bool isFullFillByTotalSize = false;//是否字节已经填满
                bool isFullFillByEntrySize = false;//是否Entry已经填满

                //perOdtEntrySize = 8; //普通CAN
                if (canfd != null) perOdtEntrySize = 64 - 1;//CANFD

                if (_config.TransportLayerType == MCModelType.XCP_ON_TCP_IP || _config.TransportLayerType == MCModelType.XCP_ON_UDP_IP)
                {
                    perOdtEntrySize = _config.ETHDTOLength.As<ushort>();
                    perOdtEntrySize -= 1;//少一一个字节
                }
                if (perOdtEntrySize <= 0) perOdtEntrySize = 7;


                var agMayNull = commonParam?.ProtocolLayer?.AddressGranularity;
                byte ag = 0;
                if (agMayNull is Asap2XCPAddressGranularity agEnum) ag = (byte)agEnum;

                var avaliableSize = totalSize;
                //去除已使用的量
                if (DaqDictionary != null)
                {
                    foreach (var item in DaqDictionary)
                    {
                        //减去DAQList所占字节数
                        avaliableSize -= perDaqSize; //本队列所占字节长度
                        if (item.Key != currentDaqName) //本队列下面的ODT长度不清理
                        {
                            //减去ODT所占字节数
                            if (item.Value != null && item.Value.Any())
                            {
                                avaliableSize -= item.Value.Count() * perOdtSize; //ODT本身占一定长度
                                avaliableSize -= item.Value.Count() * perOdtEntrySize; //每个ODT中内容的长度
                            }
                        }
                    }

                    foreach (var item in DaqDictionary)
                    {
                        //减去DAQList所占字节数
                        totalSize -= perDaqSize; //本队列所占字节长度
                        //减去ODT所占字节数
                        if (item.Value != null && item.Value.Any())
                        {
                            totalSize -= item.Value.Count() * perOdtSize; //ODT本身占一定长度
                            totalSize -= item.Value.Count() * perOdtEntrySize; //每个ODT中内容的长度
                        }
                    }
                }

                if (isAdd)
                {
                    if (totalSize < 1)
                    {
                        CommonHelper.ShowWarn(Resources.OverLoadTip + $":{currentDaqName}");
                        return null;
                    }
                }

                totalSize = avaliableSize; //本队列可用的尺寸

                if (DaqDictionary is null || ag == 0)
                {
                    CommonHelper.ShowError(Resources.A2LDataError);
                    return null;
                }

                //类型为FieldAbsolute的，ODTEntry不能超过252
                bool isDAQFieldAbsolute = (commonParam?.Daq?.IdentificationField == Asap2XCPIdentificationField.IDENTIFICATION_FIELD_TYPE_ABSOLUTE) ? true : false;
                MaxOdtLength = commonParam?.Daq == null ? 252 : (int)commonParam?.Daq?.MaxDaq;
                if (isAdd)
                {
                    //添加之前判断odtentry是否已满
                    isFullFillByEntrySize = CheckAllEntryCountIsFull(isDAQFieldAbsolute);
                    if (isFullFillByEntrySize)
                    {
                        CommonHelper.ShowError(Resources.OverLoadTip);
                        return null;
                    }

                }

                int hasDealMaxIndex = 0;//已处理到的索引


                List<ODTInfo> odts;
                //在字典中获取当前DAQList
                if (DaqDictionary.ContainsKey(currentDaqName))
                {
                    odts = DaqDictionary[currentDaqName];

                    if (isAdd)
                    {
                        //添加之前判断odtentry是否已满
                        isFullFillByEntrySize = CheckEntryCountIsFull(isDAQFieldAbsolute, odts);
                        if (isFullFillByEntrySize)
                        {
                            CommonHelper.ShowError(Resources.OverLoadTip);
                            return null;
                        }
                    }

                    odts.Clear(); //清空，从头加
                }
                else
                {
                    odts = new List<ODTInfo>();
                    DaqDictionary.Add(currentDaqName, odts);
                }

                if (measurementInfos != null && measurementInfos.Any())
                {
                    //组装数据
                    byte odtIndex = 0;
                    int hasAddSize = 0; //当前ODT中已加的信号所占字节数
                    byte entryIndex = 0; //当前ODT中已加的信号索引
                    ODTInfo odtItem = new ODTInfo()
                    {
                        ODT_NUMBER = odtIndex,
                        ODT_ENTRY = new List<ODTEntryInfo>()
                    };
                    odts.Add(odtItem);

                    //减去ODT的尺寸
                    totalSize -= perOdtSize;


                    //判断支持timestamp
                    if (isSupportTimeStamp) //支持timestamp
                    {
                        measurementInfos.Insert(0, new MeasurementItemInfo()
                        {
                            ARRAYSIZE = 1,
                            NAME = "_Timestamp_",
                            DATATYPE = XCPHelper.Asap2XCPTimeStampSizeToAsap2DataType(daq.TimeStampSupported.TimeStampSize)
                        });
                    }

                    ushort actualODTEntrySize = perOdtEntrySize;//ODTEntry实际可容纳的尺寸
                    actualODTEntrySize = ConvertHelper.As<ushort>(Math.Floor(perOdtEntrySize * 1.0 / ag) * ag);

                    //foreach (var measurement in measurementInfos)
                    for (int i = 0; i < measurementInfos.Count; i++)
                    {
                        hasDealMaxIndex = i;//处理到的标定量的索引

                        var measurement = measurementInfos[i];
                        int measurementSize = (measurement?.SIZE * (measurement?.ARRAYSIZE ?? 1) ?? 0).As<int>();

                        int itemAddSize = measurementSize;
                        if (itemAddSize < ag) itemAddSize = ag; //小于粒度的，以粒度为单位

                        uint? ecuAddress = measurement.ECU_ADDRESS;
                        if (hasAddSize + itemAddSize > actualODTEntrySize)
                        {
                            int leaveSize = measurementSize;//剩余的尺寸
                            int partIndex = 0;
                            do
                            {
                                //计算可以添加的字节量
                                int nowCanAdd = 0;
                                if (leaveSize == measurementSize) //每一次进入
                                {
                                    nowCanAdd = actualODTEntrySize - hasAddSize;
                                }
                                else //再次循环进来，hasAddSize就是0，因为在循环底部做了判断会加入新的odt
                                {
                                    if (leaveSize > actualODTEntrySize)
                                    {
                                        nowCanAdd = actualODTEntrySize;
                                    }
                                    else
                                    {
                                        nowCanAdd = leaveSize;
                                    }
                                }

                                nowCanAdd = nowCanAdd > actualODTEntrySize ? actualODTEntrySize : nowCanAdd;
                                leaveSize -= nowCanAdd; //还剩余的字节量
                                int occupantSize = ConvertHelper.As<int>(Math.Ceiling(nowCanAdd * 1.0 / ag) * ag); //占用的尺寸，当粒度为2时，3个字节也会占用4个字节
                                //其它额外判断-尺寸
                                totalSize -= occupantSize;
                                if (totalSize < 0)
                                {
                                    isFullFillByTotalSize = true;
                                    break;
                                }

                                //添加entry
                                ODTEntryInfo newEntryItem = new ODTEntryInfo()
                                {
                                    ECU_ADDRESS = ecuAddress,
                                    ECU_ADDRESS_EXTENSION = measurement.ECU_ADDRESS_EXTENSION,
                                    ELEMENT_SIZE = ConvertHelper.As<byte>(nowCanAdd),
                                    MEASUREMENT_NAME = measurement.NAME,
                                    ODT_ENTRY_NUMBER = entryIndex
                                };
                                if (leaveSize > 0 || partIndex > 0) newEntryItem.SPLIT_INDEX = partIndex;

                                odts[odtIndex].ODT_ENTRY.Add(newEntryItem);
                                entryIndex += 1;
                                partIndex += 1; //分部索引增加
                                ecuAddress = (ecuAddress ?? 0) + ConvertHelper.As<uint>(occupantSize);
                                hasAddSize += occupantSize; //已经占掉的ODT尺寸

                                //其它额外判断-entry
                                isFullFillByEntrySize = CheckEntryCountIsFull(isDAQFieldAbsolute, odts);
                                if (isFullFillByEntrySize) break; //跳出内层循环

                                if (hasAddSize == actualODTEntrySize) //已占满，且还有存余，添加ODT
                                {
                                    //&& leaveSize > 0

                                    //判断是否还能支持一个新的DOT，减去ODT的尺寸
                                    totalSize -= perOdtSize;
                                    if (totalSize < 0)
                                    {
                                        isFullFillByTotalSize = true;
                                        break;
                                    }


                                    //添加一个新的ODT
                                    odtIndex += 1;
                                    ODTInfo newOdtItem = new ODTInfo()
                                    {
                                        ODT_NUMBER = odtIndex,
                                        ODT_ENTRY = new List<ODTEntryInfo>()
                                    };
                                    odts.Add(newOdtItem);

                                    hasAddSize = 0;
                                    entryIndex = 0; //从新计数里面的entry
                                }
                            } while (leaveSize > 0);


                            //判断是否已经填满
                            if (isFullFillByTotalSize) break;  //跳出外层循环
                            if (isFullFillByEntrySize) break; //跳出外层循环

                        }
                        else
                        {
                            ODTEntryInfo entry = new ODTEntryInfo()
                            {
                                ECU_ADDRESS = ecuAddress,
                                ECU_ADDRESS_EXTENSION = measurement.ECU_ADDRESS_EXTENSION,
                                ELEMENT_SIZE = ConvertHelper.As<byte>(measurementSize),
                                MEASUREMENT_NAME = measurement.NAME
                            };

                            //其它额外判断-尺寸
                            totalSize -= itemAddSize;
                            if (totalSize < 0)
                            {
                                isFullFillByTotalSize = true;
                                break;
                            }

                            //直接放入到ODT中
                            entry.ODT_ENTRY_NUMBER = entryIndex;
                            odts[odtIndex].ODT_ENTRY.Add(entry);
                            entryIndex += 1;
                            hasAddSize += itemAddSize;

                            //其它额外判断-entry
                            isFullFillByEntrySize = CheckEntryCountIsFull(isDAQFieldAbsolute, odts);
                            if (isFullFillByEntrySize) break; //跳出循环

                            if (hasAddSize == actualODTEntrySize) //已加满，添加新的ODT,如果已经是最后一个元素，不再添加新的ODT
                            {
                                // && (i != measurementInfos.Count - 1)

                                //判断是否还能支持一个新的DOT，减去ODT的尺寸
                                totalSize -= perOdtSize;
                                if (totalSize < 0)
                                {
                                    isFullFillByTotalSize = true;
                                    break;
                                }

                                odtIndex += 1;
                                ODTInfo newOdtItem = new ODTInfo()
                                {
                                    ODT_NUMBER = odtIndex,
                                    ODT_ENTRY = new List<ODTEntryInfo>()
                                };
                                odts.Add(newOdtItem);

                                hasAddSize = 0;
                                entryIndex = 0; //从新计数里面的entry
                            }
                        }
                    }
                    //判断是否因为尺寸等问题，最后加了一个空的ODT
                    var lastOdt = odts.Last();
                    if (lastOdt.ODT_ENTRY == null || !lastOdt.ODT_ENTRY.Any())
                    {
                        odts.Remove(lastOdt);
                    }
                }
                //统计ODT数量与Entry数量
                odtCount = odts.Count;
                entryCount = odts.Sum(p => p.ODT_ENTRY?.Count() ?? 0);

                if (node != null)
                {
                    node.ODTCount = odtCount;
                    node.EntryCount = entryCount;
                }

                //判断构建结果
                if (isFullFillByTotalSize || isFullFillByEntrySize)
                {
                    if (measurementInfos != null && measurementInfos.Any())
                    {
                        if (hasDealMaxIndex < (measurementInfos.Count - 1)) //返回下一个即将出错的信号
                        {
                            return measurementInfos[hasDealMaxIndex + 1].NAME; //返回处理到的测量，已处理的会在后续移出队列
                        }
                        else //刚好处理到最后一个，处理成功
                        {
                            return BuildSuccessMsg;
                        }
                    }
                }
                else
                {
                    return BuildSuccessMsg;
                }
            }
            catch (Exception ex)
            {
                CommonHelper.LogException(ex);
            }
            return "";
        }

        string BuildCCPOdtEntryNew(List<MeasurementItemInfo> measurementInfos, string currentDaqName, bool isAdd)
        {
            try
            {
                var raster = _ccp?.Rasters?.FirstOrDefault(x => x.Name.Equals(currentDaqName) || x.ShortName.Equals(currentDaqName));
                var source = _ccp?.Sources?.FirstOrDefault(x => x.Rasters != null && x.Rasters.Any() && x.Rasters.First() == raster.ChannelNumber);
                var node = _daqEventNodes.FirstOrDefault(x => x.EVENT_CHANNEL_NAME.Equals(currentDaqName));
                if (source == null || raster == null || node == null)
                {
                    CommonHelper.ShowError(Resources.A2LDataError);
                    return null;
                }

                MeasurementItemInfo errorItem = null;
                var odtCount = source.Length;
                byte odtIndex = 0;
                byte entryIndex = 0;
                byte remianOdtSize = 7;

                List<ODTInfo> odts;

                if (DaqDictionary.ContainsKey(currentDaqName))
                {
                    odts = DaqDictionary[currentDaqName];

                    odts.Clear(); //清空，从头加
                }
                else
                {
                    odts = new List<ODTInfo>();
                    DaqDictionary.Add(currentDaqName, odts);
                }


                if (measurementInfos != null && measurementInfos.Any())
                {
                    ODTInfo odtItem = new ODTInfo()
                    {
                        ODT_NUMBER = odtIndex++,
                        ODT_ENTRY = new List<ODTEntryInfo>(),
                    };

                    var measurements = measurementInfos.ToArray();
                    foreach (var measurement in measurements)
                    {
                        byte size = (measurement?.SIZE * (measurement?.ARRAYSIZE ?? 1) ?? 0).As<byte>();
                        //判断当前ODT是否已满，如果已满7字节，则加入ODT列表，然后判断是否还有空间增加新的ODT
                        if (odtItem.ODT_ENTRY.Sum(i => i.ELEMENT_SIZE)+size>= 7)
                        {
                            odts.Add(odtItem);

                            if (odts.Count < odtCount)
                            {
                                odtItem = new ODTInfo()
                                {
                                    ODT_NUMBER = odtIndex++,
                                    ODT_ENTRY = new List<ODTEntryInfo>(),
                                };
                                entryIndex = 0;
                                remianOdtSize = 7;
                            }
                            else
                            {
                                errorItem = measurement;
                                break;
                            }
                        }

                       

                        //当前信号能直接放到ODT
                        if (size <= remianOdtSize)
                        {
                            ODTEntryInfo item = new ODTEntryInfo()
                            {
                                ECU_ADDRESS = measurement.ECU_ADDRESS,
                                ECU_ADDRESS_EXTENSION = measurement.ECU_ADDRESS_EXTENSION,
                                ELEMENT_SIZE = size,
                                MEASUREMENT_NAME = measurement.NAME,
                                ODT_ENTRY_NUMBER = entryIndex++,
                            };
                            odtItem.ODT_ENTRY.Add(item);

                            remianOdtSize -= size;
                        }//当前信号不能放到ODT，需要拆分信号到下个ODT
                        else if (odts.Sum(x => x.ODT_ENTRY.Sum(p => p.ELEMENT_SIZE)) + odtItem.ODT_ENTRY.Sum(p => p.ELEMENT_SIZE) + size < odtCount * 7)
                        {
                            byte len = size;
                            int partIndex = 0;
                            uint partAddress = measurement.ECU_ADDRESS.Value;
                            do
                            {
                                byte addSize = len > remianOdtSize ? remianOdtSize : len;

                                ODTEntryInfo partItem = new ODTEntryInfo()
                                {
                                    ECU_ADDRESS = partAddress,
                                    ECU_ADDRESS_EXTENSION = measurement.ECU_ADDRESS_EXTENSION,
                                    ELEMENT_SIZE = addSize,
                                    ODT_ENTRY_NUMBER = entryIndex++,
                                    MEASUREMENT_NAME = measurement.NAME,
                                    SPLIT_INDEX = partIndex++,
                                };

                                odtItem.ODT_ENTRY.Add(partItem);
                                remianOdtSize -= addSize;
                                partAddress += addSize;

                                if (odtItem.ODT_ENTRY.Sum(x => x.ELEMENT_SIZE) >= 7)
                                {
                                    odts.Add(odtItem);

                                    entryIndex = 0;
                                    remianOdtSize = 7;
                                    odtItem = new ODTInfo()
                                    {
                                        ODT_NUMBER = odtIndex++,
                                        ODT_ENTRY = new List<ODTEntryInfo>(),
                                    };
                                }

                                len -= addSize;

                            } while (len > 0);
                        }
                        else
                        {
                            errorItem = measurement;
                            break;
                        }
                    }

                    if (odtItem.ODT_ENTRY.Any() && !odts.Contains(odtItem))
                    {
                        odts.Add(odtItem);
                    }
                }

                node.ODTCount = odts.Count;
                node.EntryCount = odts.Sum(p => p.ODT_ENTRY?.Count() ?? 0);

                if (errorItem == null)
                {
                    return BuildSuccessMsg;
                }
                else
                {
                    return errorItem.NAME;
                }
            }
            catch (Exception ex)
            {
                CommonHelper.LogException(ex);
                return "";
            }
        }

        bool CheckAllEntryCountIsFull(bool isDAQFieldAbsolute)
        {
            bool isFullFillByEntrySize = false;
            int totalEntryCount = DaqDictionary.SelectMany(p => p.Value?.SelectMany(k => k.ODT_ENTRY)).Count();
            if (isDAQFieldAbsolute) //所有队列的Entry尺寸不能超过252
            {
                if (totalEntryCount >= MaxOdtLength) isFullFillByEntrySize = true;
            }
            else //相对的，本队列中Entry不能超过252
            {

                var rootNodeCount = DaqEventNodes?.Count ?? 0;
                if (totalEntryCount >= MaxOdtLength * rootNodeCount) isFullFillByEntrySize = true;
            }
            return isFullFillByEntrySize;
        }

        bool CheckEntryCountIsFull(bool isDAQFieldAbsolute, List<ODTInfo> odts)
        {
            bool isFullFillByEntrySize = false;
            if (isDAQFieldAbsolute) //所有队列的Entry尺寸不能超过252
            {
                int totalEntryCount = DaqDictionary.SelectMany(p => p.Value?.SelectMany(k => k.ODT_ENTRY)).Count();
                if (totalEntryCount >= MaxOdtLength)
                {
                    isFullFillByEntrySize = true;
                }
            }
            else //相对的，本队列中Entry不能超过252
            {
                int listEntryCount = odts.SelectMany(p => p.ODT_ENTRY).Count();
                if (listEntryCount >= MaxOdtLength)
                {
                    isFullFillByEntrySize = true;
                }
            }
            return isFullFillByEntrySize;
        }

        string GetDAQUsedSizePercent(out string entryUsedPercent)
        {
            entryUsedPercent = string.Empty;
            try
            {
                if (_config.TransportLayerType == MCModelType.CCP)
                {
                    var ccp = _ccp;
                    if (ccp == null || ccp.Sources is null || !ccp.Sources.Any())
                    {
                        entryUsedPercent = "0 / 0";
                        return "";
                    }

                    var all = ccp.Sources.Sum(x => x.Length * 7);
                    short usedSize = 0;

                    DaqDictionary.ForEach(daq =>
                    {
                        if (daq.Value != null && daq.Value.Any())
                        {
                            var size = daq.Value.Sum(x => x.ODT_ENTRY.Sum(p => p.ELEMENT_SIZE));
                            usedSize += size;
                        }
                    });

                    var percent = Math.Round(usedSize * 100 / (decimal)all, 1);
                    entryUsedPercent = $"{percent}% ({usedSize} / {all})";
                    return $"{percent}% ({usedSize} / {all})";
                }
                else
                {
                    var commonParam = _xcp?.CommonParameter ?? _module?.IFDataObject?.Values?.OfType<Asap2IFDataXCP>()?.FirstOrDefault()?.CommonParameter;
                    var daqMemroy = commonParam?.Daq?.DaqMemroyConsumption;
                    var totalSize = daqMemroy?.DaqMemroyLimit ?? int.MaxValue;
                    var perDaqSize = daqMemroy?.DaqSize ?? 0;
                    var perOdtSize = daqMemroy?.OdtSize ?? 0;
                    ushort perEntrySize = (ushort)(daqMemroy?.OdtEntrySize ?? 0 + daqMemroy?.OdtDaqBufferElementSize ?? 0);
                    uint usedSize = 0;
                    uint odtCount = 0;
                    uint entryCount = 0;
                    DaqDictionary.ForEach(dic =>
                    {
                        usedSize += (uint)perDaqSize;
                        dic.Value?.ForEach(o =>
                        {
                            odtCount++;
                            usedSize += (uint)perOdtSize;
                            o.ODT_ENTRY?.ForEach(e =>
                            {
                                usedSize += perEntrySize;
                                entryCount++;
                            });
                        });
                    });
                    var rootNodeCount = DaqEventNodes?.Count ?? 0;
                    var idf = commonParam.Daq?.IdentificationField;
                    int totalEntry = 0;
                    if (idf != null)
                    {
                        totalEntry = idf switch
                        {
                            Asap2XCPIdentificationField.IDENTIFICATION_FIELD_TYPE_ABSOLUTE => MaxOdtLength,
                            _ => rootNodeCount * MaxOdtLength,
                        };
                    }
                    else
                        totalEntry = rootNodeCount * MaxOdtLength;

                    if (totalEntry >= MaxOdtLength)
                    {
                        var entryPercent = Math.Round(entryCount * 100 / (decimal)totalEntry, 1);
                        entryUsedPercent = $"{entryPercent}% ({entryCount} / {totalEntry})";
                    }
                    var percent = Math.Round(usedSize * 100 / (decimal)totalSize, 1);
                    return $"{percent}% ({usedSize} / {totalSize})";
                }
            }
            catch (Exception ex)
            {
                CommonHelper.LogException(ex);
                return "0%";
            }
        }
        #region 读取当前节点的CCP/XCP配置并还原

        List<DAQEventNode> InitDAQEvent(Asap2Module module, MCModelType modelType, object tranportLayerInstace)
        {
            try
            {
                var result = new List<DAQEventNode>();

                _daqMenuItems.Clear();
                _config.DAQEventIndex ??= new Dictionary<string, int>();
                _config.DAQEventIndex.Clear();

                if(modelType == MCModelType.CCP && tranportLayerInstace is Asap2IFDataCCP ccp)
                {
                    var sources = ccp.Sources;
                    int index = 4;
                    if (sources != null && sources.Any())
                    {
                        foreach (var source in sources)
                        {
                            var rasterNode = BuildCCPEventNode(source, ccp.Rasters);
                            if (rasterNode != null)
                            {
                                rasterNode.NodeLevel = 0;
                                rasterNode.Children = new ObservableCollection<DAQEventNode>();

                                var menuItem = new MenuItemInfo
                                {
                                    Name = rasterNode.EVENT_CHANNEL_NAME,
                                    ItemCommand = new RelayCommand(sender =>
                                    {
                                        AddItemsToDaq(rasterNode);
                                    }, sender => _measurementsSfGrid?.SelectedItems?.Any() ?? false),
                                };
                                DaqMenuItems.Add(menuItem);
                                result.Add(rasterNode);
                                _config.DAQEventIndex.Add(rasterNode.EventName, index++);
                            }
                        }
                    }

                }else if(tranportLayerInstace is Asap2IFDataXCP.TransportLayerBase xcp)
                {
                    var daq = xcp.CommonParameter?.Daq ?? module.IFDataObject?.Values?.OfType<Asap2IFDataXCP>().FirstOrDefault()?.CommonParameter.Daq;
                    if(daq != null)
                    {
                        if (daq?.Events?.Any() ?? false)
                        {
                            int index = 4;
                            foreach (var daqEvent in daq.Events)
                            {
                                var xcpNode = BuildEventNode(daqEvent);
                                if (xcpNode != null)
                                {
                                    xcpNode.NodeLevel = 0;
                                    xcpNode.Children = new ObservableCollection<DAQEventNode>();

                                    var menuItem = new MenuItemInfo
                                    {
                                        Name = xcpNode.EVENT_CHANNEL_NAME,
                                        ItemCommand = new RelayCommand(sender =>
                                        {
                                            AddItemsToDaq(xcpNode);
                                        }, sender => _measurementsSfGrid?.SelectedItems?.Any() ?? false)
                                    };

                                    DaqMenuItems.Add(menuItem);
                                    result.Add(xcpNode);
                                    _config.DAQEventIndex.Add(xcpNode.EventName, index++);
                                }
                            }
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                CommonHelper.LogException(ex);
                return null;
            }

        }

        DAQEventNode BuildCCPEventNode(Asap2IFDataCCP.Source source, List<Asap2IFDataCCP.Raster> rasters)
        {
            var raster = rasters?.FirstOrDefault(x => x.ChannelNumber == source.Rasters.FirstOrDefault());
            string name = raster?.ShortName is null ? source.DaqName : raster.ShortName;
            return new DAQEventNode
            {
                EVENT_CHANNEL_NAME = name,
                EVENT_CHANNEL_NUMBER = raster.ChannelNumber,
                EVENT_CHANNEL_PRIORITY = 0,
                EventName = name,
            };
        }

        DAQEventNode BuildEventNode(Event source)
        {
            if (source is Event daqEvent)
            {
                return new DAQEventNode
                {
                    EVENT_CHANNEL_NAME = daqEvent.ChannelName,
                    EVENT_CHANNEL_NUMBER = daqEvent.ChannelNumber,
                    EVENT_CHANNEL_PRIORITY = daqEvent.Priority,
                    EVENT_CHANNEL_SHORT_NAME = daqEvent.ChannelShortName,
                    EVENT_CHANNEL_TIME_CYCLE = daqEvent.TimeCycle,
                    EVENT_CHANNEL_TIME_UNIT = daqEvent.TimeUnit,
                    EVENT_CHANNEL_TYPE = daqEvent.EventType,
                    MIN_CYCLE_TIME = daqEvent.MinCycleTime?.Cycle,
                    MIN_CYCLE_TIME_UNIT = daqEvent.MinCycleTime?.Unit,
                    MAX_DAQ_LIST = daqEvent.MaxDaqList,
                    EventName = daqEvent.ChannelName
                };
            }
            return null;
        }
        #endregion

        /// <summary>
        /// 把当前界面的测量量配置重新写入到保存Json配置的对象并保存到Json文件
        /// </summary>
        void ReWriteToConfig()
        {
            //Daq配置
            if (DaqDictionary.Any())
            {
                DaqDictionary.ForEach(daqKeyPair =>
                {
                    if (daqKeyPair.Value.Any())
                    {
                        List<MeasurementItemInfo> items = new List<MeasurementItemInfo>();

                        daqKeyPair.Value.ForEach(odtEntry =>
                        {
                            odtEntry.ODT_ENTRY.ForEach(odtEntryItem =>
                            {
                                var measurement = _allMeasurements.FirstOrDefault(x => x.NAME.Equals(odtEntryItem.MEASUREMENT_NAME));
                                if (measurement != null && !items.Contains(measurement))
                                {
                                    items.Add(measurement);
                                }
                            });
                        });

                        if (items.Any())
                        {
                            _config.SelectedDAQSignals.Add(daqKeyPair.Key, items);
                        }
                    }
                });
            }

            //Polling配置
            foreach (var polling in PollingNodes)
            {
                if (polling.Children.Any())
                {
                    var items = polling.Children.Select(x => x.MeasurementItem).ToList();
                    _config.SelectedPollingDic.Add(polling.Cycle, items);
                }
            }

            SelectViewModel.SaveConfig(_config, null);
        }
        

        /// <summary>
        /// 把当前界面的测量量的测量方式配置到环境
        /// </summary>
        async Task<bool> ConfigMeasurement()
        {
            var started = IoC.Get<IMeasurementService>().IsStarted;
            if (!started)
                return true;

            var envName = _node.Channel?.ExtendInfo["EnvironmentName"];
            var deviceSn = _node.ChannelId?.DeviceSN;
            string modelType = _config.TransportLayerType == MCModelType.CCP ? "CCP" : "XCP";

            if(string.IsNullOrEmpty(envName) || string.IsNullOrEmpty(deviceSn))
            {
                Gemini.Modules.Dialogs.MessageBox.ShowMessageForDAS(Resources.GetEnviromentInfoFailed, Resources.Failed, Gemini.Modules.Dialogs.DasMessageType.Error);
                return false;
            }

            var envProvider = IoC.Get<IHILEnv>().GetEnvironmentProvider(deviceSn, envName);
            if(envProvider is null)
            {
                Gemini.Modules.Dialogs.MessageBox.ShowMessageForDAS(Resources.GetEnviromentFailed, Resources.Failed, Gemini.Modules.Dialogs.DasMessageType.Error);
                return false;
            }

            IsBusy = true;

            StringBuilder faileds = new StringBuilder();
            int failedCount = 0;

            await Task.Run(() =>
            {
                //重置之前的配置
                if (_allMeasurements.Any())
                {
                    foreach (var measurement in _allMeasurements)
                    {
                        envProvider.WritePort(_node.Name, $"{measurement.NAME}_observe_cfg", VCar.HIL.EE.Helper.RTPC.mrt_port_type_t.MRT_INPUT_PORT, 0, 1, new Span<byte>(new byte[] { 0 }));
                    }
                }

                //Daq配置
                if (DaqDictionary.Any())
                {
                    DaqDictionary.ForEach(daqKeyPair =>
                    {
                        if (daqKeyPair.Value.Any())
                        {
                            var daqNode = DaqEventNodes.FirstOrDefault(x => x.EventName.Equals(daqKeyPair.Key));
                            if (daqNode != null)
                            {
                                byte index = (byte)(DaqEventNodes.IndexOf(daqNode) + 4);
                                daqKeyPair.Value.ForEach(odtEntry =>
                                {
                                    odtEntry.ODT_ENTRY.ForEach(odtEntryItem =>
                                    {
                                        var measurement = _allMeasurements.FirstOrDefault(x => x.NAME.Equals(odtEntryItem.MEASUREMENT_NAME));
                                        if (measurement != null)
                                        {   //do 测量量DAQ模式模型端口配置
                                            var result = envProvider.WritePort(_node.Name, $"{measurement.NAME}_observe_cfg", VCar.HIL.EE.Helper.RTPC.mrt_port_type_t.MRT_INPUT_PORT, 0, 1, new Span<byte>(new byte[] { index }));
                                            if (result < 0)
                                            {
                                                failedCount++;
                                                if (failedCount <= 20)
                                                    faileds.Append($"{measurement.NAME}\r\n");
                                            }
                                        }
                                    });
                                });
                            }
                        }
                    });
                }

                //Polling配置
                foreach (var polling in PollingNodes)
                {
                    if (polling.Children.Any())
                    {
                        var items = polling.Children.Select(x => x.MeasurementItem).ToList();

                        //do 测量量Polling模式模型端口配置

                        byte index = 1;
                        if (polling.Cycle == 500)
                            index = 2;
                        else if (polling.Cycle == 1000)
                            index = 3;


                        foreach (var measurement in items)
                        {
                            var result = envProvider.WritePort(_node.Name, $"{measurement.NAME}_observe_cfg", VCar.HIL.EE.Helper.RTPC.mrt_port_type_t.MRT_INPUT_PORT, 0, 1, new Span<byte>(new byte[] { index }));
                            if (result < 0)
                            {
                                failedCount++;
                                if (failedCount <= 20)
                                    faileds.Append($"{measurement.NAME}\r\n");
                            }
                        }
                    }
                }
            });

            IsBusy = false;

            //避免错误文本过长
            if(failedCount > 20)
            {
                faileds.Append("...");
            }

            if (faileds.Length > 0)
            {
                Gemini.Modules.Dialogs.MessageBox.ShowMessageForDAS(string.Format(Resources.MeasurementConfigFailed, faileds), Resources.Failed, Gemini.Modules.Dialogs.DasMessageType.Error);
            }
            else
            {
                Gemini.Modules.Dialogs.MessageBox.ShowMessageForDAS(Resources.Success, Resources.Success, Gemini.Modules.Dialogs.DasMessageType.Success);
            }

            return true;
        }


        void LoadMeasurementConfigurationFromXcpGroup(string filePath, List<Tuple<string, uint>> pollings, Dictionary<string, List<string>> daqs)
        {
            bool success = false;
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open))
                {
                    var serializer = new XmlSerializer(typeof(XcpConfiguration));
                    var config = serializer.Deserialize(stream);

                    if (config != null && config is XcpConfiguration configuration)
                    {
                        success = true;

                        if (configuration.Measurementgroup != null && configuration.Measurementgroup.Signals != null && configuration.Measurementgroup.Signals.Any())
                        {
                            foreach (var signal in configuration.Measurementgroup.Signals)
                            {
                                if (signal.Read.Equals("Polling") && uint.TryParse(signal.Cycle, out var cycle) && signal.Name != null)
                                {
                                    pollings.Add(new Tuple<string, uint>(signal.Name, cycle));
                                }else if(signal.Read.Equals("DAQ") && uint.TryParse(signal.Cycle, out var index) && signal.Name != null)
                                {
                                    string eventName = "";
                                    if (index <= DaqEventNodes.Count)
                                        eventName = DaqEventNodes[(int)index - 1].EventName;
                                    else
                                        eventName = index.ToString();

                                    if (daqs.ContainsKey(eventName))
                                    {
                                        daqs[eventName].Add(signal.Name);
                                    }
                                    else
                                    {
                                        daqs.Add(eventName, new List<string>() { signal.Name });
                                    }
                                }
                            }
                        }
                    }

                }
            }
            catch
            {
                success = false;
            }

            try
            {
                if (!success)
                {
                    XDocument doc = XDocument.Load(filePath);

                    var signals = doc.Descendants("signal");

                    foreach (var signal in signals)
                    {
                        string name = signal.Attribute("name").Value;
                        string cycle = signal.Attribute("cycle").Value;
                        string read = signal.Attribute("read").Value;

                        if (name is null || cycle is null || read is null)
                            continue;

                        if (read.Equals("Polling") && uint.TryParse(cycle, out var cycleValue))
                        {
                            pollings.Add(new Tuple<string, uint>(name, cycleValue));
                        }
                        else if (read.Equals("DAQ") && uint.TryParse(cycle, out var index))
                        {
                            string eventName = "";
                            if (index <= DaqEventNodes.Count)
                                eventName = DaqEventNodes[(int)index - 1].EventName;
                            else
                                eventName = index.ToString();

                            if (daqs.ContainsKey(eventName))
                            {
                                daqs[eventName].Add(name);
                            }
                            else
                            {
                                daqs.Add(eventName, new List<string>() { name });
                            }
                        }
                    }
                }
            }
            catch
            {

            }
        }

        void LoadMeasurementConfigurationFromJson(string filePath, List<Tuple<string, uint>> pollings, Dictionary<string, List<string>> daqs)
        {
            try
            {
                string lines = File.ReadAllText(filePath);

                MeasurementConfiguration configuration = JsonConvert.DeserializeObject<MeasurementConfiguration>(lines);

                if(configuration is null || configuration.Signals is null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Gemini.Modules.Dialogs.MessageBox.ShowMessageForDAS(Entity.Properties.XCPModelResources.ConfigurationFileNotValid, Entity.Properties.XCPModelResources.Error, Gemini.Modules.Dialogs.DasMessageType.Error);

                    });
                    return;
                }

                if(configuration.Signals.Any())
                {
                    var ps = configuration.Signals.Where(x => x.EventName is null).ToList();
                    foreach(var signal in ps)
                    {
                        pollings.Add(new Tuple<string, uint>(signal.Name, signal.Cycle));
                    }

                    var ds = configuration.Signals.Where(x => x.EventName != null).ToList();
                    foreach (var signal in ds)
                    {
                        if (daqs.ContainsKey(signal.EventName))
                        {
                            daqs[signal.EventName].Add(signal.Name);
                        }
                        else
                        {
                            daqs.Add(signal.EventName, new List<string>() { signal.Name });
                        }
                    }
                }
            }
            catch
            {

            }
        }

        void ApplayMeasurementConfigration(List<Tuple<string, uint>> pollings, Dictionary<string, List<string>> daqs)
        {
            try
            {
                string tip = "";
                List<string> mismatching = new List<string>();
                List<string> incompatibility = new List<string>();
                List<string> notInEvent = new List<string>();
                List<string> inUsing = new List<string>();

                Dictionary<string, MeasurementItemInfo> allMeasurements = new Dictionary<string, MeasurementItemInfo>();
                Dictionary<string, MeasurementItemInfo> measurements = new Dictionary<string, MeasurementItemInfo>();
                foreach (var m in _allMeasurements)
                {
                    if (!allMeasurements.ContainsKey(m.NAME))
                        allMeasurements.Add(m.NAME, m);
                }

                foreach (var m in _measurements)
                {
                    if (!measurements.ContainsKey(m.NAME))
                        measurements.Add(m.NAME, m);
                }

                if (pollings != null && pollings.Any())
                {
                    Dictionary<int, List<MeasurementItemInfo>> pollingDict = new Dictionary<int, List<MeasurementItemInfo>>();

                    foreach (var item in pollings)
                    {
                        allMeasurements.TryGetValue(item.Item1, out var m);
                        if (m == null)
                        {
                            mismatching.Add(item.Item1);
                            continue;
                        }

                        if (!measurements.ContainsKey(item.Item1))
                        {
                            inUsing.Add(item.Item1);
                            continue;
                        }

                        if (item.Item2 == 100 || item.Item2 == 500 || item.Item2 == 1000)
                        {
                            if (pollingDict.ContainsKey((int)item.Item2))
                            {
                                pollingDict[(int)item.Item2].Add(m);
                            }
                            else                                                                // 当前Polling定义仅有100 500 1000
                            {
                                pollingDict.Add((int)item.Item2, new List<MeasurementItemInfo>() { m });
                            }
                        }
                        else
                        {
                            incompatibility.Add(item.Item1);
                        }
                    }

                    foreach (var polling in pollingDict)
                    {
                        AddPolling(polling.Value, polling.Key);
                    }
                }

                if (daqs.Any())
                {
                    Dictionary<DAQEventNode, List<MeasurementItemInfo>> daqDict = new Dictionary<DAQEventNode, List<MeasurementItemInfo>>();
                    foreach (var daq in daqs)
                    {
                        List<MeasurementItemInfo> ms = new List<MeasurementItemInfo>();

                        foreach (var item in daq.Value)
                        {
                            allMeasurements.TryGetValue(item, out var m);
                            if (m is null)
                            {
                                mismatching.Add(item);
                                continue;
                            }

                            if (!measurements.ContainsKey(item))
                            {
                                inUsing.Add(item);
                                continue;
                            }

                            ms.Add(m);
                        }

                        var eventNodes = DaqEventNodes.FirstOrDefault(x => x.EventName.Equals(daq.Key));
                        if (eventNodes is null)
                        {
                            foreach (var item in ms)
                            {
                                incompatibility.Add(item.NAME);
                            }

                            continue;
                        }

                        if (ms.Any())
                            daqDict.Add(eventNodes, ms);
                    }

                    GetDAQUsedSizePercent(out string entryPercent);
                    if (entryPercent.StartsWith("100%") && daqDict.Any())
                    {
                        tip += $"{XCPModelResources.OverLoadTip}\r\n";

                        int index = 0;
                        foreach(var keyPair in daqDict)
                        {
                            foreach(var item in keyPair.Value)
                            {
                                tip += $"{item.NAME}\r\n";
                                if(index >= 4)
                                {
                                    tip += "...\r\n";
                                    break;
                                }

                                index++;
                            }
                        }
                    }
                    else
                    {
                        foreach (var keyPair in daqDict)
                        {
                            AddItemsToDaq(keyPair.Key, keyPair.Value);
                        }
                    }
                }
                
                if (mismatching.Any() || incompatibility.Any() || notInEvent.Any() || inUsing.Any())
                {

                    if (mismatching.Any())
                    {
                        tip += $"{XCPModelResources.SignalNotFound}[{mismatching.Count}]\r\n";

                        int index = 0;
                        foreach (var item in mismatching)
                        {
                            tip += $"{item}\r\n";
                            if (index >= 4)
                            {
                                tip += "...\r\n";
                                break;
                            }

                            index++;
                        }
                    }

                    if (inUsing.Any())
                    {
                        tip += $"{XCPModelResources.ConfigInUsing}[{inUsing.Count}]\r\n";

                        int index = 0;
                        foreach(var item in inUsing)
                        {
                            tip += $"{item}\r\n";
                            if(index >= 4)
                            {
                                tip += "...\r\n";
                                break;
                            }

                            index++;
                        }
                    }

                    if (incompatibility.Any())
                    {
                        tip += $"{XCPModelResources.CycleNotMatch}[{incompatibility.Count}]\r\n";

                        int index = 0;
                        foreach(var item in incompatibility)
                        {
                            tip += $"{item}\r\n";
                            if (index >= 4)
                            {
                                tip += "...\r\n";
                                break;
                            }

                            index++;
                        }
                    }

                    if (notInEvent.Any())
                    {
                        tip += $"{XCPModelResources.DaqNotFound}[{notInEvent.Count}]\r\n";

                        int index = 0;
                        foreach (var item in notInEvent)
                        {
                            tip += $"{item}\r\n";
                            if (index >= 4)
                            {
                                tip += "...\r\n";
                                break;
                            }

                            index++;
                        }
                    }

                    Gemini.Modules.Dialogs.MessageBox.ShowMessageForDAS(tip, Entity.Properties.XCPModelResources.Error, Gemini.Modules.Dialogs.DasMessageType.Error);
                }
            }
            catch
            {

            }
        }

        void AddItemsToDaq(DAQEventNode node, List<MeasurementItemInfo> items)
        {
            var existItems = node.Children.Select(n => n.MeasurementItem).ToList();
            var allExitItems = DaqEventNodes.SelectMany(d => d.Children).Select(c => c.MeasurementItem).ToList();

            var addItems = items.Distinct(new MeasurementCompare());

            //找出地址为0的项来
            var zeroAddressItems = addItems.Where(p => (p.ECU_ADDRESS ?? 0) == 0).ToList();
            if (zeroAddressItems != null && zeroAddressItems.Any())
            {
                addItems = addItems.Except(zeroAddressItems).ToList();
                CommonHelper.ShowWarn(string.Format(Resources.ZeroCannotAddDAQ, zeroAddressItems.Count));
            }

            var filterItems = addItems.Where(item => !allExitItems.Select(exItem => exItem.NAME).Contains(item.NAME)).ToList();
            var repeatItems = addItems.Where(item => allExitItems.Select(exItem => exItem.NAME).Contains(item.NAME)).Select(item => item.NAME).ToList();
            
            existItems.AddRange(filterItems);
            //var result = BuildDynamicOdtEntry(existItems, rootNode.EVENT_CHANNEL_SHORT_NAME);
            var result = BuildOdtEntryNew(existItems, node.EVENT_CHANNEL_NAME, true);

            //构建完DAQ之后的操作
            ActionAfterBuildODTItem(result, node, filterItems, true);

            RefreshSourceGrid();

            RefreshFilter(_measurementsSfGrid);
            DAQChanged();
        }
    }
}

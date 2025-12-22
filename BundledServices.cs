using Caliburn.Micro;
using Gemini.Framework.Commands;
using Gemini.Modules.ProjectManager;
using Kunyi.DAS2.Common.Interface.Project;
using Kunyi.DAS2.Diagnostics.Entity.Entity;
using Kunyi.DAS2.Diagnostics.Entity.Helper;
using Kunyi.Parser.Pdx;
using Org.BouncyCastle.Asn1.Pkcs;
using Syncfusion.UI.Xaml.Charts;
using Syncfusion.XlsIO.Implementation;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Kunyi.DAS2.Diagnostics.Entity.Entitys.Flash
{
    public enum MotorolaByteOrderEnum
    {
        /// <summary>
        /// 英特尔
        /// </summary>
        Intel = 0,
        /// <summary>
        /// 摩托罗拉
        /// </summary>
        Motorola = 1
    }
    public enum DataBlockEraseTypeEnum
    {
        /// <summary>
        /// 无
        /// </summary>
        None = 0,
        /// <summary>
        /// 擦除HEX对应地址
        /// </summary>
        HexAddressBlockErase = 1,
        /// <summary>
        /// 下载下一个数据块前擦除相应块
        /// </summary>
        EraseCorrespondingBlockBeforeDownloading = 2

    }
    public enum BinFlashType
    {
        Default = 0,
        GAC_App_Binary = 1,
        GAC_Flash_Binary = 2
    }
    public enum CommandTypeEnum
    {
        /// <summary>
        /// 不校验
        /// </summary>
        None = 0,
        /// <summary>
        /// ECU端校验
        /// </summary>
        ChecksumAtECU = 1,
        /// <summary>
        /// 用户自定义
        /// </summary>
        UserDefine = 2,
        /// <summary>
        /// PC端校验
        /// </summary>
        ChecksumAtPc = 3
    }
    public enum BlockChecksumTypeEnum
    {
        /// <summary>
        /// 不校验
        /// </summary>
        None,
        /// <summary>
        /// 校验每一个数据块
        /// </summary>
        CheckEveryDataBlock = 1,
        /// <summary>
        /// 校验每一个数据块Catl
        /// </summary>
        CheckEveryDataBlockCatl = 2,
        /// <summary>
        /// 校验31+RoutineID
        /// </summary>
        CHeck31RoutineID = 3
    }
    public class GroupService : INotifyPropertyChanged
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        private string name { get; set; } = "下载文件";
        /// <summary>
        /// 服务名称
        /// </summary>
        public string Name
        {
            get => name; set
            {
                name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        private ObservableCollection<BundledServices> _groupServices;

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<BundledServices> ChildrenServices
        {
            get => _groupServices;
            set
            {
                if (_groupServices != value)
                {
                    _groupServices = value;
                    OnPropertyChanged(nameof(ChildrenServices));
                }
            }
        }

        // 触发属性更改通知
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    public class DataBlock : INotifyPropertyChanged, System.ICloneable
    {
        // 静态检测当前文化是否为中文（仅初始化一次，提升性能）
        private static readonly bool _isChinese = CultureInfo.CurrentCulture.Name
            .Trim()
            .ToLowerInvariant()
            .StartsWith("zh");

        private bool _isSelected;
        private int _name;
        private string _startAddress = string.Empty;
        private string _endAddress = string.Empty;
        private int _length;
        private string _checksum = string.Empty;
        private string _mapAddress = string.Empty;
        private List<byte> _data = new List<byte>();
        /// <summary>
        /// 属性变更通知事件
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 是否选中
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
        /// <summary>
        /// 当前类容
        /// </summary>
        public List<byte> Data
        {
            get => _data;
            set => SetProperty(ref _data, value);
        }
        /// <summary>
        /// 数据块编号
        /// </summary>
        public int Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>
        /// 数据块名称显示（多语言）
        /// </summary>
        public string NameDisplay => _isChinese
            ? $"   #   数据块 {Name}"
            : $"   #   DataBlock {Name}";

        /// <summary>
        /// 起始地址
        /// </summary>
        public string StartAddress
        {
            get => _startAddress;
            set => SetProperty(ref _startAddress, value);
        }

        /// <summary>
        /// 起始地址显示（多语言）
        /// </summary>
        public string StartAddressDisplay => _isChinese
            ? $"起始地址: {StartAddress}"
            : $"Starting Address: {StartAddress}";

        /// <summary>
        /// 结束地址
        /// </summary>
        public string EndAddress
        {
            get => _endAddress;
            set => SetProperty(ref _endAddress, value);
        }

        /// <summary>
        /// 结束地址显示（多语言，修正翻译）
        /// </summary>
        public string EndAddressDisplay => _isChinese
            ? $"结束地址: {EndAddress}"
            : $"End Address: {EndAddress}";

        /// <summary>
        /// 数据长度
        /// </summary>
        public int Length
        {
            get => _length;
            set => SetProperty(ref _length, value);
        }

        /// <summary>
        /// 数据长度显示（多语言）
        /// </summary>
        public string LengthDisplay => _isChinese
            ? $"数据长度: 0x{Length:X8} = {Length}"
            : $"Data Length: 0x{Length:X8} = {Length}";

        /// <summary>
        /// 校验和
        /// </summary>
        public string Checksum
        {
            get => _checksum;
            set => SetProperty(ref _checksum, value);
        }

        /// <summary>
        /// 校验和显示（多语言）
        /// </summary>
        public string ChecksumDisplay => _isChinese
            ? $"校验和: {Checksum}"
            : $"Checksum: {Checksum}";

        /// <summary>
        /// 映射地址
        /// </summary>
        public string MapAddress
        {
            get => _mapAddress;
            set => SetProperty(ref _mapAddress, value);
        }

        /// <summary>
        /// 映射地址显示（多语言，优化空值处理）
        /// </summary>
        public string MapAddressDisplay
        {
            get
            {
                var value = string.IsNullOrWhiteSpace(MapAddress) ? "N/A" : MapAddress;
                return _isChinese
                    ? $"映射地址: {value}"
                    : $"Mapping Address: {value}";
            }
        }

        /// <summary>
        /// 属性变更设置通用方法
        /// </summary>
        /// <typeparam name="T">属性类型</typeparam>
        /// <param name="storage">字段引用</param>
        /// <param name="value">新值</param>
        /// <param name="propertyName">属性名（自动填充）</param>
        /// <returns>是否变更成功</returns>
        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = "")
        {
            if (Equals(storage, value)) return false;

            storage = value;
            OnPropertyChanged(propertyName);

            // 通知依赖属性变更（如 Display 类属性）
            NotifyDependentProperties(propertyName);

            return true;
        }

        /// <summary>
        /// 触发属性变更通知
        /// </summary>
        /// <param name="propertyName">属性名</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 通知依赖属性变更（核心属性变更时，同步更新 Display 属性）
        /// </summary>
        /// <param name="changedProperty">变更的核心属性名</param>
        private void NotifyDependentProperties(string changedProperty)
        {
            switch (changedProperty)
            {
                case nameof(Name):
                    OnPropertyChanged(nameof(NameDisplay));
                    break;
                case nameof(StartAddress):
                    OnPropertyChanged(nameof(StartAddressDisplay));
                    break;
                case nameof(EndAddress):
                    OnPropertyChanged(nameof(EndAddressDisplay));
                    break;
                case nameof(Length):
                    OnPropertyChanged(nameof(LengthDisplay));
                    break;
                case nameof(Checksum):
                    OnPropertyChanged(nameof(ChecksumDisplay));
                    break;
                case nameof(MapAddress):
                    OnPropertyChanged(nameof(MapAddressDisplay));
                    break;
            }
        }
        /// <summary>
        /// 实现 ICloneable 接口，返回一个全新的深克隆对象。
        /// </summary>
        /// <returns>数据块的深层副本。</returns>
        public object Clone()
        {
            return DeepClone();
        }

        /// <summary>
        /// 创建 DataBlock 的深层副本。
        /// </summary>
        /// <returns>DataBlock 的新实例，包含所有值的副本，特别是 Data 列表的副本。</returns>
        public DataBlock DeepClone()
        {
            DataBlock newBlock = new DataBlock
            {
                _isSelected = this._isSelected,
                _name = this._name,
                _startAddress = this._startAddress,
                _endAddress = this._endAddress,
                _length = this._length,
                _checksum = this._checksum,
                _mapAddress = this._mapAddress,

                _data = this._data != null
                        ? this._data.ToList().Clone()
                        : new List<byte>()
            };

            return newBlock;
        }
    }
    public class BundledServices : INotifyPropertyChanged
    {
        public string Id { get; set; }
        protected BundledServices() { }
        // 业务构造：手动指定Id或生成新Guid
        public BundledServices(string id)
        {
            Id = id ?? Guid.NewGuid().ToString("N"); 
        }

        private string name { get; set; } = CultureInfo.CurrentCulture.Name.ToLower().StartsWith("zh") ? "下载文件1" : "download file 1";
        /// <summary>
        /// 服务名称
        /// </summary>
        public string Name
        {
            get => name; set
            {
                name = value;
                OnPropertyChanged();
            }
        }
        private bool ecu_suport_zip = false;
        public bool EcuSupportZip
        {
            get => ecu_suport_zip;
            set
            {
                ecu_suport_zip = value;
                OnPropertyChanged();
            }
        }

        // 新增树形结构属性
        private bool _isRoot = true;
        /// <summary>
        /// 是否为根节点
        /// </summary>
        public bool IsRoot
        {
            get => _isRoot;
            set { _isRoot = value; OnPropertyChanged(); }
        }
        private BinFlashType binFlashType = BinFlashType.Default;
        public BinFlashType BinFlash
        {
            get => binFlashType;
            set
            {
                binFlashType = value;
                OnPropertyChanged();
            }
        }
        private string binSartAdd = "00000000";
        public string BinStartAdd
        {
            set
            {
                binSartAdd = value;
                OnPropertyChanged();
            }
            get => binSartAdd;
        }


        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private ObservableCollection<DataBlock> blockList = new ObservableCollection<DataBlock>();
        public ObservableCollection<DataBlock> DataBlock
        {
            get => blockList;
            set
            {
                blockList = value;
                OnPropertyChanged();
            }
        }
        private ObservableCollection<DataBlock> blockRawList = new ObservableCollection<DataBlock>();
        public ObservableCollection<DataBlock> DataRawBlock
        {
            get => blockRawList;
            set
            {
                blockRawList = value;
                OnPropertyChanged();
            }
        }
        //private ObservableCollection<BundledServices> _childrenServices;
        ///// <summary>
        ///// 子节点集合
        ///// </summary>
        //public ObservableCollection<BundledServices> ChildrenServices
        //{
        //    get => _childrenServices ??= new ObservableCollection<BundledServices>();
        //    set { _childrenServices = value; OnPropertyChanged(); }
        //}
        private int storageMemoryAddress = 4;
        /// <summary>
        /// 存储内存地址字节数
        /// </summary>
        public int StorageMemoryAddress
        {
            get => storageMemoryAddress;
            set
            {
                storageMemoryAddress = value;
                OnPropertyChanged();
            }
        }
        private int storageMemorySize = 4;
        /// <summary>
        /// 存储内存大小字节数
        /// </summary>
        public int StorageMemorySize
        {
            get => storageMemoryAddress; set
            {
                storageMemoryAddress = value;
                OnPropertyChanged();
            }
        }
        public MotorolaByteOrderEnum motorolaByteOrder = MotorolaByteOrderEnum.Intel;
        /// <summary>
        /// 字节序
        /// </summary>
        public MotorolaByteOrderEnum MotorolaByteOrder
        {
            get => motorolaByteOrder;
            set
            {
                motorolaByteOrder = value;
                OnPropertyChanged();
            }

        }
        /// <summary>
        /// 字节序数字
        /// </summary>
        public int MotorolaByteOrderIndex
        {
            get => (int)MotorolaByteOrder;
            set
            {
                MotorolaByteOrder = (MotorolaByteOrderEnum)value;
                OnPropertyChanged();
            }

        }
        private bool checkEntireFileAndData = false;
        /// <summary>
        /// 检查整个文件数据和内部数据块是否使用不同的校验和算法
        /// </summary>
        public bool CheckEntireFileAndData
        {
            get => checkEntireFileAndData;
            set
            {
                checkEntireFileAndData = value;
                OnPropertyChanged();
            }
        }

        private CrcAlgorithm crcParameters = new CrcAlgorithm("CRC32", 32, "04C11DB7", "FFFFFFFF", "FFFFFFFF");
        /// <summary>
        /// 校验和
        /// </summary>
        public CrcAlgorithm CrcParameters
        {
            get => crcParameters;
            set
            {
                crcParameters = value;
                OnPropertyChanged();
            }
        }
        private DataBlockEraseTypeEnum dataBlockEraseType = DataBlockEraseTypeEnum.None;
        /// <summary>
        /// 数据块擦除类型
        /// </summary>
        public DataBlockEraseTypeEnum DataBlockEraseType
        {
            get => dataBlockEraseType;
            set
            {
                dataBlockEraseType = value;
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// 数据块擦除类型数字
        /// </summary>
        public int DataBlockEraseTypeIndex
        {
            get => (int)DataBlockEraseType;
            set
            {
                DataBlockEraseType = (DataBlockEraseTypeEnum)value;
                OnPropertyChanged();
            }
        }
        private string routineIDStr = "FF00";
        /// <summary>
        /// RoutineID十六进制字符串
        /// </summary>
        public string RoutineIDStr
        {
            get => routineIDStr;
            set
            {
                routineIDStr = value;
                OnPropertyChanged();
            }
        }
        private bool entireS44 = false;
        /// <summary>
        /// 擦除 $44
        /// </summary>
        public bool EntireS44
        {
            get => entireS44;
            set
            {
                entireS44 = value;
                OnPropertyChanged();
            }
        }
        private string expectedReturnValue = "00";
        /// <summary>
        /// 期望返回值
        /// </summary>
        public string ExpectedReturnValue
        {
            get => expectedReturnValue;
            set
            {
                expectedReturnValue = value;
                OnPropertyChanged();
            }
        }
        private string requestTransmissionDataCommandDataFormat = "00";
        /// <summary>
        /// 请求传输数据命令数据格式 (0x)
        /// </summary>
        public string RequestTransmissionDataCommandDataFormat
        {
            get => requestTransmissionDataCommandDataFormat;
            set
            {
                requestTransmissionDataCommandDataFormat = value;
                OnPropertyChanged();
            }

        }
        public void Cacl_Crc()
        {
            if (DataRawBlock != null && DataRawBlock.Count > 0)
            {
                List<byte> allData = new List<byte>();
                foreach (var block in DataRawBlock)
                {
                    allData.AddRange(block.Data);

                    var crcVal = CrcCalculator.Compute(block.Data.ToArray(), CrcParameters);
                    string crcHex = "";
                    if (CrcParameters.BitWidth == 32) crcHex = $"0x{crcVal:X8}";
                    else if (CrcParameters.BitWidth == 16) crcHex = $"0x{crcVal:X4}";
                    else crcHex = $"0x{crcVal:X2}";
                    block.Checksum = crcHex;
                }
                AllCrcByte = CrcCalculator.ComputeBytes(allData.ToArray(), CrcParameters);

            }
            if (DataBlock != null && DataBlock.Count > 0)
            {

                foreach (var block in DataBlock)
                {

                    var crcVal = CrcCalculator.Compute(block.Data.ToArray(), CrcParameters);
                    string crcHex = "";
                    if (CrcParameters.BitWidth == 32) crcHex = $"0x{crcVal:X8}";
                    else if (CrcParameters.BitWidth == 16) crcHex = $"0x{crcVal:X4}";
                    else crcHex = $"0x{crcVal:X2}";
                    block.Checksum = crcHex;
                }
            }
        }
        public void calczijie()
        {
            if (string.IsNullOrWhiteSpace(hexpath))
            {
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    DataRawBlock.Clear();
                    DataBlock.Clear();
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        DataRawBlock.Clear();
                        DataBlock.Clear();
                    });

                }
            }
            else
            {
                //Application.Current.Dispatcher.BeginInvoke(() =>
                //{
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    DataRawBlock.Clear();
                    DataBlock.Clear();
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        DataRawBlock.Clear();
                        DataBlock.Clear();
                    });

                }
                SortedDictionary<uint, byte> rawData = new SortedDictionary<uint, byte>();
                List<DataBlock> blocks = new List<DataBlock>();
                try
                {
                    var ext = Path.GetExtension(hexpath).ToLower();
                    var project = IoC.Get<IProject>();
                    var projectM = IoC.Get<IProjectManager>();
                    var path = System.IO.Path.Combine(project.GetModuleRootPath("Diagnostics"), $"{projectM.GetCurrentProjectInfo().ProjectName}", "Flash", hexpath);
                    if (ext == ".hex")
                    {
                        rawData = ParseIntelHex(path);
                        blocks = GenerateBlocks(rawData);

                    }
                    else if (ext == ".s19")
                    {
                        rawData = ParseMotorolaS19(path);
                        blocks = GenerateBlocks(rawData);
                    }
                    else if (ext == ".bin")
                    {
                        if (BinFlash == BinFlashType.Default)
                        {
                            rawData = BinMapper.ParseBIN(path, (uint)HexStringToIntConverter.ConvertHexStringWithSpaceToInt(BinStartAdd));
                            blocks = GenerateBlocks(rawData);
                        }
                        else if (BinFlash == BinFlashType.GAC_Flash_Binary)
                        {
                            rawData = BinMapper.BuildGacFlash(path, 1);
                            blocks = GenerateBlocks(rawData);
                        }
                        else
                        {
                            rawData = BinMapper.BuildGacApp(path);
                            blocks = GenerateBlocks(rawData);
                        }

                    }
                    else if (ext == ".vbf")
                    {
                        VbfSetting vbfConfig = new VbfSetting();
                        rawData = ParseVBF(path, ref vbfConfig);
                        blocks = GenerateBlocks(rawData);
                    }
                    else
                    {
                        rawData = ParseZIP(path);
                        blocks = GenerateBlocks(rawData);

                    }

                }
                catch (Exception e)
                {
                    HexPath = "";
                    DataBlock.Clear();
                    FileErrMsg = $"{e.Message}";
                }
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    int index = 0;
                    foreach (var block in blocks)
                    {
                        block.Name = index++;
                        DataRawBlock.Add(block);
                    }
                    uint splitSize = 0;
                    if (uint.TryParse(SupportSplittingFlashBlockAreaSize.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint parsedSize))
                    {
                        splitSize = parsedSize;
                    }
                    if (DataRawBlock != null && DataRawBlock.Count > 0)
                    {
                        List<byte> allData = new List<byte>();
                        foreach (var block in DataRawBlock)
                        {
                            allData.AddRange(block.Data);

                            var crcVal = CrcCalculator.ComputeBytes(block.Data.ToArray(), CrcParameters);
                            string crcHex = "";
                            if (CrcParameters.BitWidth == 32) crcHex = $"0x{crcVal:X8}";
                            else if (CrcParameters.BitWidth == 16) crcHex = $"0x{crcVal:X4}";
                            else crcHex = $"0x{crcVal:X2}";
                        }
                        AllCrcByte = CrcCalculator.ComputeBytes(allData.ToArray(), CrcParameters);

                    }
                    if (SupportSplittingFlashBlockArea && splitSize > 0)
                    {
                        DataBlock = SplitBlocks(DataRawBlock, splitSize);
                    }
                    else
                    {
                        DataBlock.Clear();
                        foreach (var rawBlock in DataRawBlock)
                        {
                            DataBlock.Add((DataBlock)rawBlock.Clone());
                        }
                        OnPropertyChanged(nameof(DataBlock));
                    }
                    OnPropertyChanged(nameof(DataBlock));
                }
                else
                {
                    // 如果在后台线程，使用Dispatcher将更新操作转交给UI线程
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        int index = 0;
                        foreach (var block in blocks)
                        {
                            block.Name = index++;
                            DataRawBlock.Add(block);
                        }
                        uint splitSize = 0;
                        if (uint.TryParse(SupportSplittingFlashBlockAreaSize.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint parsedSize))
                        {
                            splitSize = parsedSize;
                        }
                        if (DataRawBlock != null && DataRawBlock.Count > 0)
                        {
                            List<byte> allData = new List<byte>();
                            foreach (var block in DataRawBlock)
                            {
                                allData.AddRange(block.Data);

                                var crcVal = CrcCalculator.ComputeBytes(block.Data.ToArray(), CrcParameters);
                                string crcHex = "";
                                if (CrcParameters.BitWidth == 32) crcHex = $"0x{crcVal:X8}";
                                else if (CrcParameters.BitWidth == 16) crcHex = $"0x{crcVal:X4}";
                                else crcHex = $"0x{crcVal:X2}";
                            }
                            AllCrcByte = CrcCalculator.ComputeBytes(allData.ToArray(), CrcParameters);

                        }
                        if (SupportSplittingFlashBlockArea && splitSize > 0)
                        {
                            DataBlock = SplitBlocks(DataRawBlock, splitSize);
                        }
                        else
                        {
                            DataBlock.Clear();
                            foreach (var rawBlock in DataRawBlock)
                            {
                                DataBlock.Add((DataBlock)rawBlock.Clone());
                            }
                            OnPropertyChanged(nameof(DataBlock));
                        }
                        OnPropertyChanged(nameof(DataBlock));
                    });
                }
                //});
            }
        }
        private string fileErrmsg = "";
        public string FileErrMsg
        {
            get => fileErrmsg;
            set
            {
                fileErrmsg = value;
                OnPropertyChanged();
            }
        }
        private bool allowUserDdefinedMaximumLength = false;
        /// <summary>
        /// 使能允许用户自定义传输数据块的最大长度
        /// </summary>
        public bool AllowUserDdefinedMaximumLength
        {
            get => allowUserDdefinedMaximumLength;
            set
            {
                allowUserDdefinedMaximumLength = value;
                OnPropertyChanged();
            }

        }
        private string allowUserDdefinedMaximumLengthDataLength = "202";
        /// <summary>
        /// 允许用户自定义传输数据块的最大长度 (0x):
        /// </summary>
        public string AllowUserDdefinedMaximumLengthDataLength
        {
            get => allowUserDdefinedMaximumLengthDataLength;
            set
            {
                allowUserDdefinedMaximumLengthDataLength = value;
                OnPropertyChanged();
            }
        }
        private int timeIntervalAfterRequestCommand = 1;
        /// <summary>
        /// 请求命令后的时间间隔
        /// </summary>
        public int TimeIntervalAfterRequestCommand
        {
            get => timeIntervalAfterRequestCommand;
            set
            {
                timeIntervalAfterRequestCommand = value;
                OnPropertyChanged();
            }

        }
        private int timeIntervalAfterTransmissionDataCommand = 1;
        /// <summary>
        /// 传输数据命令后的时间间隔
        /// </summary>
        public int TimeIntervalAfterTransmissionDataCommand
        {
            get => timeIntervalAfterTransmissionDataCommand;
            set
            {
                timeIntervalAfterTransmissionDataCommand = value;
                OnPropertyChanged();
            }

        }
        public CommandTypeEnum commandType = CommandTypeEnum.None;
        /// <summary>
        /// 37命令类型
        /// </summary>
        public CommandTypeEnum CommandType
        {
            get => commandType;
            set
            {
                commandType = value;
                OnPropertyChanged();
            }

        }
        private byte[] allCrcByte = new byte[4];
        /// <summary>
        /// 整个数据的crc结果
        /// </summary>
        public byte[] AllCrcByte
        {
            get => allCrcByte;
            set => allCrcByte = value;
        }


        /// <summary>
        /// 37命令类型数字
        /// </summary>
        public int CommandTypeIndex
        {
            get => (int)CommandType;
            set
            {
                CommandType = (CommandTypeEnum)value;
            }
        }
        private string pdu37 = "37";
        /// <summary>
        /// 37 PDU(0x)
        /// </summary>
        public string Pdu37
        {
            get => pdu37;
            set
            {
                pdu37 = value;
                OnPropertyChanged();
            }

        }
        private BlockChecksumTypeEnum blockChecksumType = BlockChecksumTypeEnum.None;
        /// <summary>
        /// 块校验和 (S3101 + ID + 校验和)
        /// </summary>
        public BlockChecksumTypeEnum BlockChecksumType
        {
            get => blockChecksumType;
            set
            {
                blockChecksumType = value;
                OnPropertyChanged();
            }

        }
        /// <summary>
        ///  块校验和 (S3101 + ID + 校验和)数字
        /// </summary>
        public int BlockChecksumTypeIndex
        {
            get => (int)BlockChecksumType;
            set
            {
                BlockChecksumType = (BlockChecksumTypeEnum)value;
            }
        }
        private string routineID3101 = "0202";
        /// <summary>
        /// S3101 RoutineID十六进制字符串
        /// </summary>
        public string RoutineID3101
        {
            get => routineID3101;
            set
            {
                routineID3101 = value;
                OnPropertyChanged();
            }
        }
        private bool transmissionS44 = false;
        /// <summary>
        /// 传输 $44
        /// </summary>
        public bool TransmissionS44
        {
            get => transmissionS44;
            set
            {
                transmissionS44 = value;
                OnPropertyChanged();
            }

        }
        private string transmissionExpectedReturnValue = "00";
        /// <summary>
        /// 传输期望返回值
        /// </summary>
        public string TransmissionExpectedReturnValue
        {
            get => transmissionExpectedReturnValue;
            set
            {
                transmissionExpectedReturnValue = value;
                OnPropertyChanged();
            }


        }
        private int timeIntervalAfterTransmissionExitCommand = 1;
        /// <summary>
        /// 传输退出命令后的时间间隔
        /// </summary>
        public int TimeIntervalAfterTransmissionExitCommand
        {
            get => timeIntervalAfterTransmissionExitCommand;
            set
            {
                timeIntervalAfterTransmissionExitCommand = value;
                OnPropertyChanged();
            }
        }
        public bool supportSplittingFlashBlockArea = false;
        /// <summary>
        /// 支持分割Flash块区域
        /// </summary>
        public bool SupportSplittingFlashBlockArea
        {
            get => supportSplittingFlashBlockArea;
            set
            {
                supportSplittingFlashBlockArea = value;
                OnPropertyChanged();
                uint splitSize = 0;
                if (uint.TryParse(SupportSplittingFlashBlockAreaSize.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint parsedSize))
                {
                    splitSize = parsedSize;
                }

                if (SupportSplittingFlashBlockArea && splitSize > 0)
                {
                    DataBlock = SplitBlocks(DataRawBlock, splitSize);
                }
                else
                {
                    DataBlock.Clear();
                    foreach (var rawBlock in DataRawBlock)
                    {
                        DataBlock.Add((DataBlock)rawBlock.Clone());
                    }
                    OnPropertyChanged(nameof(DataBlock));
                }
                OnPropertyChanged(nameof(DataBlock));
            }
        }
        public bool IsHexStringNoRegex(string input, bool allowPrefix = true, bool allowSpace = false, bool requireEvenLength = false)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            string cleanInput = input.Trim();

            // 移除前缀
            if (allowPrefix && (cleanInput.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || cleanInput.StartsWith("0X", StringComparison.OrdinalIgnoreCase)))
            {
                cleanInput = cleanInput.Substring(2);
                if (string.IsNullOrWhiteSpace(cleanInput))
                    return false;
            }

            int validCharCount = 0;
            foreach (char c in cleanInput)
            {
                // 允许空格（仅统计有效字符）
                if (allowSpace && c == ' ')
                    continue;

                // 校验是否为16进制字符
                bool isHexChar = (c >= '0' && c <= '9') ||
                                 (c >= 'A' && c <= 'F') ||
                                 (c >= 'a' && c <= 'f');
                if (!isHexChar)
                    return false;

                validCharCount++;
            }

            // 校验有效字符长度（偶数位要求）
            if (requireEvenLength && validCharCount % 2 != 0)
                return false;

            // 至少有1个有效字符
            return validCharCount > 0;
        }
        private string supportSplittingFlashBlockAreaSize = "10000";
        /// <summary>
        /// 支持分割Flash块区域大小（0x）
        /// </summary>
        public string SupportSplittingFlashBlockAreaSize
        {
            get => supportSplittingFlashBlockAreaSize;
            set
            {
                if (IsHexStringNoRegex(value))
                {
                    supportSplittingFlashBlockAreaSize = value;
                    OnPropertyChanged();
                    uint splitSize = 0;
                    if (uint.TryParse(SupportSplittingFlashBlockAreaSize.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint parsedSize))
                    {
                        splitSize = parsedSize;
                    }
                    if (SupportSplittingFlashBlockArea && splitSize > 0)
                    {
                        DataBlock.Clear();
                        DataBlock = SplitBlocks(DataRawBlock, splitSize);
                        OnPropertyChanged(nameof(DataBlock));
                    }
                    else
                    {
                        DataBlock.Clear();
                        foreach (var rawBlock in DataRawBlock)
                        {
                            DataBlock.Add((DataBlock)rawBlock.Clone());
                        }
                        OnPropertyChanged(nameof(DataBlock));
                    }
                }


            }
        }
        private string hexpath = "";
        public string HexPath
        {
            get => hexpath;
            set
            {
                hexpath = value;
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// 解析文件入口，返回一个排序好的字典 <地址, 字节值>
        /// </summary>
        private SortedDictionary<uint, byte> ParseFirmwareFile(string path)
        {
            var ext = Path.GetExtension(path).ToLower();
            if (ext == ".hex")
                return ParseIntelHex(path);
            else if (ext == ".s19" || ext == ".srec")
                return ParseMotorolaS19(path);
            else
                throw new Exception("不支持的文件格式");
        }
        private SortedDictionary<uint, byte> ParseIntelHex(string path)
        {
            var memory = new SortedDictionary<uint, byte>();
            string[] lines = File.ReadAllLines(path);
            uint upperAddress = 0; // 用于处理扩展线性地址记录 (Type 04)

            foreach (var line in lines)
            {
                if (!line.StartsWith(":")) continue;

                // 移除冒号，解析字节
                byte[] bytes = HexStringToByteArray(line.Substring(1));
                byte byteCount = bytes[0];
                ushort address = (ushort)((bytes[1] << 8) + bytes[2]);
                byte recordType = bytes[3];
                // bytes[4] 开始是数据
                // 最后一个字节是 Checksum

                if (recordType == 0x00) // 数据记录
                {
                    uint currentBase = upperAddress + address;
                    for (int i = 0; i < byteCount; i++)
                    {
                        memory[currentBase + (uint)i] = bytes[4 + i];
                    }
                }
                else if (recordType == 0x04) // 扩展线性地址记录
                {
                    // 改变基地址的高16位
                    upperAddress = (uint)((bytes[4] << 8) + bytes[5]) << 16;
                }
                else if (recordType == 0x01) // 文件结束
                {
                    break;
                }
            }
            return memory;
        }
        /// <summary>
        /// 解析 ZIP 文件流（原始二进制模式）。
        /// </summary>
        /// <param name="zipStream">ZIP 文件的原始流（整体二进制流）</param>
        /// <param name="startAddress">预设的起始地址（默认 0x00000000）</param>
        /// <returns>地址-字节映射的有序字典</returns>
        private SortedDictionary<uint, byte> ParseZIP(string path)
        {
            uint startAddress = 0x00000000;
            using (var zipStream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                var memory = new SortedDictionary<uint, byte>();

                // 校验流有效性
                if (zipStream == null || !zipStream.CanRead)
                {
                    throw new ArgumentException("ZIP 文件流无效或不可读", nameof(zipStream));
                }

                // 1. 将整个 ZIP 流读取为原始字节数组（对齐 ParseBIN 的 File.ReadAllBytes 逻辑）
                byte[] data = ReadStreamToByteArray(zipStream);

                // 2. 将每个字节按地址顺序存入内存字典（完全复用 ParseBIN 的核心逻辑）
                for (int i = 0; i < data.Length; i++)
                {
                    memory[startAddress + (uint)i] = data[i];
                }

                return memory;
            }
        }

        /// <summary>
        /// 辅助方法：将任意流读取为字节数组（替代 File.ReadAllBytes，适配流场景）
        /// </summary>
        /// <param name="stream">待读取的流</param>
        /// <returns>流对应的字节数组</returns>
        private byte[] ReadStreamToByteArray(Stream stream)
        {
            // 若流是 MemoryStream，直接转换以提升性能
            if (stream is MemoryStream ms)
            {
                return ms.ToArray();
            }

            // 其他流（FileStream/NetworkStream 等）：逐块读取
            using (var tempMs = new MemoryStream())
            {
                byte[] buffer = new byte[4096]; // 4KB 缓冲区，平衡性能与内存
                int bytesRead;
                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    tempMs.Write(buffer, 0, bytesRead);
                }
                return tempMs.ToArray();
            }
        }

        /// <summary>
        /// 字节数组转uint（强制大端序，支持任意有效长度的字节数组）
        /// </summary>
        /// <param name="bytes">输入字节数组（长度1-4，超出部分截断）</param>
        /// <returns>大端序解析的uint值</returns>
        /// <exception cref="ArgumentNullException">字节数组为空</exception>
        /// <exception cref="ArgumentException">字节数组长度为0</exception>
        public static uint ByteArrayToUInt32BigEndian(byte[] bytes)
        {
            // 空值/空数组校验
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes), "字节数组不能为空");
            if (bytes.Length == 0)
                throw new ArgumentException("字节数组长度不能为0", nameof(bytes));

            // 步骤1：补齐为4字节（uint固定4字节），不足补0（大端序补高位）
            byte[] fourBytes = new byte[4];
            int copyLength = Math.Min(bytes.Length, 4);
            // 大端序：数据从高位（数组起始）开始填充，不足则低位补0
            Array.Copy(bytes, 0, fourBytes, 4 - copyLength, copyLength);

            // 步骤2：强制按大端序转换（BinaryPrimitives是.NET Core 2.1+/NET 5+推荐API）
            return BinaryPrimitives.ReadUInt32BigEndian(fourBytes);
        }
        /// <summary>
        /// 解析 VBF 文件，并将其数据段整合到通用的内存字典中。
        /// </summary>
        private SortedDictionary<uint, byte> ParseVBF(string path, ref VbfSetting vbfConfig)
        {
            var memory = new SortedDictionary<uint, byte>();

            try
            {
                // 1. 调用专业的 VBF 解析工具类
                vbfConfig = VbfParser.ReadVbfFile(path);

                // 2. 遍历 VBF 中解析出的所有数据段 (SegmentList)
                foreach (var segment in vbfConfig.SegmentList)
                {
                    // VBF 规范中地址 StartAddr 是 4 字节大端序 (Big-Endian)
                    // 您的解析器已经读取为 byte[]，我们需要将其转换为 uint 地址
                    uint startAddress = ByteArrayToUInt32BigEndian(segment.StartAddr);

                    uint currentAddress = startAddress;

                    // 3. 遍历数据段中的所有数据包 (MessagePacket)
                    foreach (var packet in segment.SegData)
                    {
                        // 4. 将包内数据逐字节写入内存字典
                        foreach (byte dataByte in packet.DataList)
                        {
                            memory[currentAddress] = dataByte;
                            currentAddress++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 捕获 VBF 解析器内部可能抛出的各种异常（文件格式错误、头部参数缺失等）
                throw new FormatException($"VBF 文件解析错误: {ex.Message}", ex);
            }

            return memory;
        }
        /// <summary>
        /// 将 4 字节数组转换为 uint (32位地址)。
        /// </summary>
        /// <param name="bytes">4 字节数组</param>
        /// <param name="isLittleEndian">是否使用小端序（VBF使用大端序，应为 False）</param>
        /// <returns>32位无符号整数地址</returns>
        private uint ByteArrayToUint(byte[] bytes)
        {
            if (bytes == null || bytes.Length != 4)
                throw new ArgumentException("地址必须是 4 字节数组。");
            bool isLittleEndian = MotorolaByteOrder == MotorolaByteOrderEnum.Intel ? true : false;
            if (isLittleEndian)
            {
                // 如果是小端序 (Intel)
                return (uint)(bytes[3] << 24 | bytes[2] << 16 | bytes[1] << 8 | bytes[0]);
            }
            else
            {
                // 默认 VBF 使用大端序 (Motorola)
                return (uint)(bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3]);
            }
        }
        private SortedDictionary<uint, byte> ParseMotorolaS19(string path)
        {
            //var memory = new SortedDictionary<uint, byte>();
            //string[] lines = File.ReadAllLines(path);

            //foreach (var line in lines)
            //{
            //    if (!line.StartsWith("S")) continue;

            //    char type = line[1];
            //    if (type == '0' || type == '5' || type == '7' || type == '8' || type == '9') continue; // 跳过头、尾和计数记录

            //    // S1: 16-bit address (2 bytes)
            //    // S2: 24-bit address (3 bytes)
            //    // S3: 32-bit address (4 bytes)
            //    int addressBytesLen = (type == '1') ? 2 : (type == '2') ? 3 : 4;

            //    // 字节计数 (包含地址和校验和)
            //    byte count = Convert.ToByte(line.Substring(2, 2), 16);

            //    // 提取地址
            //    string addrHex = line.Substring(4, addressBytesLen * 2);
            //    uint address = Convert.ToUInt32(addrHex, 16);

            //    // 计算数据的长度 = 总计数 - 地址长度 - 1(校验和)
            //    int dataLen = count - addressBytesLen - 1;

            //    // 提取数据
            //    int dataStartIndex = 4 + (addressBytesLen * 2);
            //    string dataHex = line.Substring(dataStartIndex, dataLen * 2);
            //    byte[] dataBytes = HexStringToByteArray(dataHex);

            //    for (int i = 0; i < dataBytes.Length; i++)
            //    {
            //        memory[address + (uint)i] = dataBytes[i];
            //    }
            //}
            //return memory;
            var memory = new SortedDictionary<uint, byte>();
            string[] lines = File.ReadAllLines(path);

            uint? currentAddress = null;
            var currentData = new List<byte>();

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || !line.StartsWith("S"))
                    continue;

                char recordType = line[1];
                if (recordType != '1' && recordType != '2' && recordType != '3')
                    continue; // 只处理 S1/S2/S3 数据记录

                try
                {
                    int byteCount = Convert.ToInt32(line.Substring(2, 2), 16);
                    int addrLenBytes = recordType == '1' ? 2 : recordType == '2' ? 3 : 4;
                    int addrLenHex = addrLenBytes * 2;

                    if (line.Length < 4 + addrLenHex + 2) // 头部 + 地址 + 至少1字节数据 + 校验和
                        continue;

                    string addrHex = line.Substring(4, addrLenHex);
                    uint address = Convert.ToUInt32(addrHex, 16);

                    int dataStartIndex = 4 + addrLenHex;
                    int dataHexLength = (byteCount - addrLenBytes - 1) * 2;
                    if (line.Length < dataStartIndex + dataHexLength + 2) continue; // +校验和

                    string dataHex = line.Substring(dataStartIndex, dataHexLength);
                    byte[] data = HexStringToByteArray(dataHex);

                    // 严格按 Python 逻辑：检查是否连续
                    bool isContinuation = currentAddress.HasValue &&
                                          address == currentAddress.Value + (uint)currentData.Count;

                    if (!isContinuation)
                    {
                        // 不连续：保存当前块，开始新块
                        if (currentData.Count > 0 && currentAddress.HasValue)
                        {
                            uint start = currentAddress.Value;
                            for (int i = 0; i < currentData.Count; i++)
                            {
                                memory[start + (uint)i] = currentData[i];
                            }
                        }

                        currentAddress = address;
                        currentData.Clear();
                    }

                    // 追加数据
                    currentData.AddRange(data);
                }
                catch
                {
                    // 异常时跳过行，和 Python 一样
                    continue;
                }
            }

            // 保存最后一个块
            if (currentData.Count > 0 && currentAddress.HasValue)
            {
                uint start = currentAddress.Value;
                for (int i = 0; i < currentData.Count; i++)
                {
                    memory[start + (uint)i] = currentData[i];
                }
            }

            return memory;
        }

        // ==========================================
        // 辅助方法：合并连续内存块 & CRC计算
        // ==========================================

        private List<DataBlock> GenerateBlocks(SortedDictionary<uint, byte> memory)
        {
            var blocks = new List<DataBlock>();
            if (memory.Count == 0) return blocks;

            var keys = memory.Keys.ToList();

            uint startAddr = keys[0];
            uint prevAddr = keys[0];
            List<byte> currentBlockData = new List<byte> { memory[startAddr] };

            for (int i = 1; i < keys.Count; i++)
            {
                uint currAddr = keys[i];

                // 如果地址不连续，说明是一个新的块
                if (currAddr != prevAddr + 1)
                {
                    // 保存上一个块
                    blocks.Add(CreateBlock(startAddr, prevAddr, currentBlockData));

                    // 开启新块
                    startAddr = currAddr;
                    currentBlockData = new List<byte>();
                }

                currentBlockData.Add(memory[currAddr]);
                prevAddr = currAddr;
            }

            // 添加最后一个块
            blocks.Add(CreateBlock(startAddr, prevAddr, currentBlockData));

            return blocks;
        }
        /// <summary>
        /// 根据最大尺寸拆分 DataBlock 列表。
        /// </summary>
        /// <param name="rawBlocks">未拆分的原始块列表 (DataRawBlock)</param>
        /// <param name="maxSize">最大块大小</param>
        /// <returns>拆分后的新块列表</returns>
        private ObservableCollection<DataBlock> SplitBlocks(ObservableCollection<DataBlock> rawBlocks, uint maxSize)
        {
            if (maxSize == 0) return new ObservableCollection<DataBlock>(rawBlocks);

            var newBlocks = new ObservableCollection<DataBlock>();
            int newIndex = 0;

            foreach (var rawBlock in rawBlocks)
            {
                // 确保原始块有数据
                if (rawBlock.Data == null || rawBlock.Data.Count == 0)
                {
                    rawBlock.Name = newIndex++;
                    newBlocks.Add(rawBlock);
                    continue;
                }

                uint startAddr;
                if (!uint.TryParse(rawBlock.StartAddress.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture, out startAddr))
                {
                    rawBlock.Name = newIndex++;
                    newBlocks.Add(rawBlock);
                    continue;
                }

                uint currentStart = startAddr;
                int remainingLength = rawBlock.Length;
                int currentOffset = 0; // 当前数据在 Data 列表中的偏移量

                while (remainingLength > 0)
                {
                    // 确定当前块的长度，不超过剩余长度和最大尺寸
                    int currentLength = (int)Math.Min(remainingLength, maxSize);

                    // 从原始数据中截取当前块的数据
                    List<byte> chunkData = rawBlock.Data
                        .Skip(currentOffset)
                        .Take(currentLength)
                        .ToList();

                    // 计算新块的结束地址
                    uint currentEnd = currentStart + (uint)currentLength - 1;

                    // 重新计算校验和
                    uint crcVal = CrcCalculator.Compute(chunkData.ToArray(), CrcParameters);
                    string crcHex = "";
                    if (CrcParameters.BitWidth == 32) crcHex = $"0x{crcVal:X8}";
                    else if (CrcParameters.BitWidth == 16) crcHex = $"0x{crcVal:X4}";
                    else crcHex = $"0x{crcVal:X2}";

                    // 创建新的 DataBlock
                    newBlocks.Add(new DataBlock
                    {
                        IsSelected = rawBlock.IsSelected,
                        Name = newIndex++,
                        StartAddress = $"0x{currentStart:X8}",
                        EndAddress = $"0x{currentEnd:X8}",
                        Length = currentLength,
                        Checksum = crcHex,
                        MapAddress = rawBlock.MapAddress,
                        Data = chunkData // 存储拆分后的数据
                    });

                    // 更新下一个块的起始地址、剩余长度和偏移量
                    currentStart += (uint)currentLength;
                    remainingLength -= currentLength;
                    currentOffset += currentLength;
                }
            }

            return newBlocks;
        }
        /// <summary>
        /// 这是一个演示如何根据字节序读取数据的辅助方法。
        /// 它将传入的4个字节按照当前设置的字节序组成一个32位整数。
        /// </summary>
        private uint ReadUint32(byte[] dataBytes, int offset)
        {
            if (dataBytes.Length < offset + 4)
            {
                // 数据不足以组成32位整数
                return 0;
            }

            // 读取4个字节
            byte b0 = dataBytes[offset];
            byte b1 = dataBytes[offset + 1];
            byte b2 = dataBytes[offset + 2];
            byte b3 = dataBytes[offset + 3];

            if (MotorolaByteOrder == MotorolaByteOrderEnum.Intel) // Little-Endian (低位字节在前)
            {
                // 示例： b0 b1 b2 b3 -> 0x b3 b2 b1 b0
                return (uint)(b3 << 24 | b2 << 16 | b1 << 8 | b0);
            }
            else // Motorola (Big-Endian, 高位字节在前)
            {
                // 示例： b0 b1 b2 b3 -> 0x b0 b1 b2 b3
                return (uint)(b0 << 24 | b1 << 16 | b2 << 8 | b3);
            }
        }
        private DataBlock CreateBlock(uint start, uint end, List<byte> data)
        {
            //ushort crc = CalculateCRC16(data.ToArray());
            uint crcVal = CrcCalculator.Compute(data.ToArray(), CrcParameters);
            // 根据位宽格式化输出
            string crcHex = "";
            if (CrcParameters.BitWidth == 32) crcHex = $"0x{crcVal:X8}";
            else if (CrcParameters.BitWidth == 16) crcHex = $"0x{crcVal:X4}";
            else crcHex = $"0x{crcVal:X2}";
            //string mapAddressDisplay = "映射地址: Na";
            //if (data.Count >= 4)
            //{
            //    try
            //    {
            //        uint firstWord = ReadUint32(data.ToArray(), 0);

            //        // 根据当前的字节序，第一个字的值会不同
            //        mapAddressDisplay = $"映射地址: 0x{firstWord:X8}";
            //    }
            //    catch
            //    {
            //        mapAddressDisplay = "映射地址: Na";
            //    }
            //}
            return new DataBlock
            {
                IsSelected = true,
                StartAddress = $"0x{start:X8}",
                EndAddress = $"0x{end:X8}",
                Length = data.Count,
                Checksum = crcHex,
                MapAddress = "",
                Data = data
            };
        }

        // 简单的 16进制字符串转字节数组
        private byte[] HexStringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        // CRC16-CCITT (Poly: 0x1021, Initial: 0xFFFF) - 这是嵌入式常用的标准
        private ushort CalculateCRC16(byte[] data)
        {
            ushort crc = 0xFFFF; // 初始值，有的算法是0x0000，根据需要修改
            foreach (byte b in data)
            {
                crc ^= (ushort)(b << 8);
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x8000) != 0)
                        crc = (ushort)((crc << 1) ^ 0x1021);
                    else
                        crc <<= 1;
                }
            }
            return crc;
        }
    }
}

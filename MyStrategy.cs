using Aicup2020.Model;
using System.Collections.Generic;

namespace Aicup2020
{
    public class MyStrategy
    {
        Dictionary<int, EntityMemory> entityMemories = new Dictionary<int, EntityMemory>();

        #region Служебные переменные
        static EntityType[] entityTypesArray = new EntityType[]{ EntityType.BuilderUnit, EntityType.RangedUnit, EntityType.MeleeUnit,
            EntityType.Turret, EntityType.House, EntityType.BuilderBase, EntityType.MeleeBase, EntityType.RangedBase, EntityType.Wall,
            EntityType.Resource };

        bool needPrepare = true;
        #endregion

        #region клетки где можно построить юнита вокруг здания
        int largeBuildingSize = 5;
        int[] buildingPositionDX = { -1, -1, -1, -1, -1, 0, 1, 2, 3, 4, 5, 5, 5, 5, 5, 4, 3, 2, 1, 0 };
        int[] buildingPositionDY = { 0, 1, 2, 3, 4, 5, 5, 5, 5, 5, 4, 3, 2, 1, 0, -1, -1, -1, -1, -1 };
        int[] buildingPositionIter = { 0, 1, -1, 2, -2, 3, -3, 4, -4, 5, -5, 6, -6, 7, -7, 8, -8, 9, -9, 10 };
        // позиции расположены вот так
        //   5  6  7  8  9  
        // 4                10
        // 3                11
        // 2                12
        // 1                13
        // 0  X             14
        //   19 18 17 16 15
        #endregion

        Dictionary<EntityType, Group> basicEntityIdGroups = new Dictionary<EntityType, Group>();
        Group groupHouseBuilders = new Group();
        Group groupRepairBuilders = new Group();
        Group groupRetreatBuilders = new Group();
        Group groupMyBuildersAttackEnemyBuilders = new Group();

        List<int> needRepairBuildingIdList = new List<int>();
        List<int> needRepairUnitsIdList = new List<int>();

        enum DebugOptions
        {
            canDrawGetAction, drawBuildBarrierMap, drawBuildAndRepairOrder, drawBuildAndRepairPath, drawRetreat,
            drawOnceVisibleMap, drawRangedBasePotencPlace,
            drawInteresMap, drawMemoryResources, drawMemoryEnemies,
            drawPotencAttackOverMy, drawPotencAttackAll, drawPotencAttackMove, drawPotencAttackPathfind, drawPotencTarget5Map,
            drawOptAttack, drawPlanedKill, drawOptRangerMove, drawOptRangerPathfind,
            drawOrderStatistics, drawDeadCellMap,
            drawDeadStatistic,
            canDrawDebugUpdate, allOptionsCount
        }
        bool[] debugOptions = new bool[(int)DebugOptions.allOptionsCount];

        PlayerView _playerView;
        DebugInterface _debugInterface;
        static IDictionary<EntityType, EntityProperties> properties;

        System.Random random = new System.Random();

        static int[][] cellWithIdOnlyBuilding;
        static int[][] cellWithIdAny;
        static int[][] nextPositionMyUnitsMap;
        int[][] onceVisibleMap;
        bool[][] currentVisibleMap;
        static int[][] resourceMemoryMap;
        int[][] resourcePotentialField;
        const int RPFmyBuildingWeight = -10;
        const int RPFdangerCellWeight = -6;
        const int RPFenemyEntityWeight = -2;
        const int RPFdeniedBuilderWeight = 1;
        const int RPFdeniedUnitWeight = -1;
        const int RPFwarningCellWeight = 2;
        class BuildMapCell
        {
            public bool s2canBuildNow;
            public bool s2canBuildAfter;
            public bool s2noBaseOrWarriorBarrier;
            public bool s2noBuilderBarrier;
            public bool s2noEnemiesBarrier;
            public bool s2noUnvisivleBarrier;
            public int s2howManyResBarrier;

            public bool s3canBuildNow;
            public bool s3canBuildAfter;
            public bool s3noBaseOrWarriorBarrier;
            public bool s3noBuilderBarrier;
            public bool s3noEnemiesBarrier;
            public bool s3noUnvisivleBarrier;
            public int s3howManyResBarrier;

            public bool s5canBuildNow;
            public bool s5canBuildAfter;
            public bool s5noBaseOrWarriorBarrier;
            public bool s5noBuilderBarrier;
            public bool s5noEnemiesBarrier;
            public bool s5noUnvisivleBarrier;
            public int s5howManyResBarrier;

            public BuildMapCell()
            {
                Reset();
            }

            public void Check()
            {
                if (s2noBaseOrWarriorBarrier
                    && s2noEnemiesBarrier
                    && s2howManyResBarrier == 0
                    && s2noUnvisivleBarrier)
                {
                    s2canBuildAfter = true;
                    if (s2noBuilderBarrier)
                    {
                        s2canBuildNow = true;
                    }
                }
                if (s3noBaseOrWarriorBarrier
                    && s3noEnemiesBarrier
                    && s3howManyResBarrier == 0
                    && s3noUnvisivleBarrier)
                {
                    s3canBuildAfter = true;
                    if (s3noBuilderBarrier)
                    {
                        s3canBuildNow = true;
                    }
                }
                if (s5noBaseOrWarriorBarrier
                    && s5noEnemiesBarrier
                    && s5howManyResBarrier == 0
                    && s5noUnvisivleBarrier)
                {
                    s5canBuildAfter = true;
                    if (s5noBuilderBarrier)
                    {
                        s5canBuildNow = true;
                    }
                }
            }

            public void Reset()
            {
                s2canBuildNow = s3canBuildNow = s5canBuildNow = false;
                s2canBuildAfter = s3canBuildAfter = s5canBuildAfter = false;
                s2noBaseOrWarriorBarrier = s3noBaseOrWarriorBarrier = s5noBaseOrWarriorBarrier = true;
                s2noBuilderBarrier = s3noBuilderBarrier = s5noBuilderBarrier = true;
                s2noEnemiesBarrier = s3noEnemiesBarrier = s5noEnemiesBarrier = true;
                s2howManyResBarrier = s3howManyResBarrier = s5howManyResBarrier = 0;
                s2noUnvisivleBarrier = s3noUnvisivleBarrier = s5noUnvisivleBarrier = true;
            }

            public int HowManyResBarrier(int size)
            {
                switch (size)
                {
                    case 2: return s2howManyResBarrier;
                    case 3: return s3howManyResBarrier;
                    case 5: return s5howManyResBarrier;
                    default:
                        break;
                }
                return 0;
            }

            public bool CanBuildNow(int size)
            {
                switch (size)
                {
                    case 2: return s2canBuildNow;
                    case 3: return s3canBuildNow;
                    case 5: return s5canBuildNow;
                    default:
                        break;
                }
                return false;
            }
            public bool CanBuildAfter(int size)
            {
                switch (size)
                {
                    case 2: return s2canBuildAfter;
                    case 3: return s3canBuildAfter;
                    case 5: return s5canBuildAfter;
                    default:
                        break;
                }
                return false;
            }
        }
        BuildBarrierMap buildBarrierMap;
        class BuildBarrierMap
        {
            BuildMapCell[,] _buildBarrierMap;
            int _mapSize;

            public BuildBarrierMap(int size)
            {
                _mapSize = size;
                _buildBarrierMap = new BuildMapCell[_mapSize, _mapSize];
                for (int x = 0; x < _mapSize; x++)
                {
                    for (int y = 0; y < _mapSize; y++)
                    {
                        _buildBarrierMap[x, y] = new BuildMapCell();
                    }

                }
            }
            public void Reset()
            {
                for (int x = 0; x < _mapSize; x++)
                {
                    for (int y = 0; y < _mapSize; y++)
                    {
                        _buildBarrierMap[x, y].Reset();
                    }

                }
            }
            public enum BlockVariant {Enemy, Builder, MyBuilding, Unvisible };
            public void BlockCell(int x, int y, BlockVariant variant)
            {
                for (int dx = -4; dx <= 0; dx++)
                {
                    for (int dy = -4; dy <= 0; dy++)
                    {
                        bool s2 = dx > -2 && dy > -2;
                        bool s3 = dx > -3 && dy > -3;
                        bool s5 = true;

                        int nx = x + dx;
                        int ny = y + dy;
                        if (nx >= 0 && nx < _mapSize && ny >= 0 && ny < _mapSize)
                        {
                            switch (variant)
                            {
                                case BlockVariant.Enemy:
                                    if (s2) _buildBarrierMap[nx, ny].s2noEnemiesBarrier = false;
                                    if (s3) _buildBarrierMap[nx, ny].s3noEnemiesBarrier = false;
                                    if (s5) _buildBarrierMap[nx, ny].s5noEnemiesBarrier = false;
                                    break;
                                case BlockVariant.Builder:
                                    if (s2) _buildBarrierMap[nx, ny].s2noBuilderBarrier = false;
                                    if (s3) _buildBarrierMap[nx, ny].s3noBuilderBarrier = false;
                                    if (s5) _buildBarrierMap[nx, ny].s5noBuilderBarrier = false;
                                    break;
                                case BlockVariant.MyBuilding:
                                    if (s2) _buildBarrierMap[nx, ny].s2noBaseOrWarriorBarrier = false;
                                    if (s3) _buildBarrierMap[nx, ny].s3noBaseOrWarriorBarrier = false;
                                    if (s5) _buildBarrierMap[nx, ny].s5noBaseOrWarriorBarrier = false;
                                    break;
                                case BlockVariant.Unvisible:
                                    if (s2) _buildBarrierMap[nx, ny].s2noUnvisivleBarrier = false;
                                    if (s3) _buildBarrierMap[nx, ny].s3noUnvisivleBarrier = false;
                                    if (s5) _buildBarrierMap[nx, ny].s5noUnvisivleBarrier = false;
                                    break;
                                default:
                                    throw new System.Exception("unknown variant");
                            }
                        }
                    }
                }
            }
            public void AddResource(int x, int y)
            {
                for (int dx = -4; dx <= 0; dx++)
                {
                    for (int dy = -4; dy <= 0; dy++)
                    {
                        bool s2 = dx > -2 && dy > -2;
                        bool s3 = dx > -3 && dy > -3;
                        bool s5 = true;

                        int nx = x + dx;
                        int ny = y + dy;
                        if (nx >= 0 && nx < _mapSize && ny >= 0 && ny < _mapSize)
                        {
                            if (s2) _buildBarrierMap[nx, ny].s2howManyResBarrier++;
                            if (s3) _buildBarrierMap[nx, ny].s3howManyResBarrier++;
                            if (s5) _buildBarrierMap[nx, ny].s5howManyResBarrier++;                            
                        }
                    }
                }
            }
            public BuildMapCell this[int x, int y]
            {
                get
                {
                    if (x >= 0 && x < _mapSize && y >= 0 && y < _mapSize)
                        return _buildBarrierMap[x, y];
                    else
                        return null;
                }
                set
                {
                    if (x >= 0 && x < _mapSize && y >= 0 && y < _mapSize)
                        _buildBarrierMap[x, y] = value;
                }
            }
        }
        class InteresQuad
        {
            public int X1 { get; private set; }
            public int Y1 { get; private set; }
            public int X2 { get; private set; }
            public int Y2 { get; private set; }
            public bool _isEnemyBase;
            //int _lastEnemyBaseTick;
            public bool _isMyBorder;
            public bool _isMyTerritory;
            int _lastMyTerritoryTick;
            public bool _isEnemyWarriors;
            public bool _isEnemyBuilders;
            int _lastEnemyWarriorTick;
            public bool _isExplored;
            int _lastExploreTick;

            public InteresQuad(int xx1, int yy1, int xx2, int yy2, int lastExploreTick)
            {
                X1 = xx1;
                Y1 = yy1;
                X2 = xx2;
                Y2 = yy2;
                _isEnemyBuilders = false;
                _isEnemyBase = false;
                _isMyBorder = false;
                _isMyTerritory = false;
                _lastMyTerritoryTick = -1;
                _isEnemyWarriors = false;
                //_lastEnemyBaseTick = -1;
                _lastEnemyWarriorTick = -1;
                _isExplored = false;
                _lastExploreTick = lastExploreTick;
            }

            public void NextTick(int currentTick, bool isMyTerritory, bool isEnemyBase, int enemyMelees, int enemyRangers, int enemyBuilders, int visibleCells)
            {
                _isMyBorder = false; // проверяем это на втором этапе
                if (isMyTerritory)
                {
                    _isMyTerritory = isMyTerritory;
                    _lastMyTerritoryTick = currentTick;
                }
                if (isEnemyBase)
                {
                    _isEnemyBase = isEnemyBase;
                } else
                {
                    if (visibleCells >= InteresMap.numberVisCellsForEnemyBase)//сбрасывать флаг вражеской базы, только если убедились в отсутствии врага на % клеток
                    {
                        _isEnemyBase = isEnemyBase;
                    }
                }

                if (enemyMelees + enemyRangers > 0){
                    _isEnemyWarriors = true;
                    _lastEnemyWarriorTick = currentTick;
                }         
                if (enemyBuilders > 0)
                {
                    _isEnemyBuilders = true;
                } else
                {
                    if (visibleCells >= InteresMap.numberVisCellsForEnemyBase)//сбрасывать флаг вражеской базы, только если убедились в отсутствии врага на % клеток
                    {
                        _isEnemyBuilders = false;
                    }
                }
                if (visibleCells >= InteresMap.numberVisCellsForExplore)
                {
                    _isExplored = true;
                    _lastExploreTick = currentTick;
                }

                if (currentTick - _lastEnemyWarriorTick > InteresMap.memoryAttackDuration)
                    _isEnemyWarriors = false;
                if (currentTick - _lastExploreTick > InteresMap.memoryExploreDuration)
                    _isExplored = false;
                if (currentTick - _lastMyTerritoryTick > InteresMap.memoryMyDuration)
                    _isMyTerritory = false;
            }
        }
        class InteresMap
        {
            InteresQuad[,] interesQuads;
            public const int quadSize = 8;
            public const int memoryExploreDuration = 200;
            public const int memoryAttackDuration = 30;
            public const int memoryMyDuration = 50;
            const float percVisForExplore = 0.6f;
            public const int numberVisCellsForExplore = (int)(quadSize * quadSize * percVisForExplore);
            const float percVisForEnemyBase = 0.9f;
            public const int numberVisCellsForEnemyBase = (int)(quadSize * quadSize * percVisForEnemyBase);

            public int cellCount { get; private set; }
            int mapSize;

            public InteresMap(int mapSize)
            {
                this.mapSize = mapSize;
                cellCount = mapSize / quadSize;
                interesQuads = new InteresQuad[cellCount, cellCount];
                for (int x = 0; x < cellCount; x++)
                {
                    for (int y = 0; y < cellCount; y++)
                    {
                        interesQuads[x, y] = new InteresQuad(x * quadSize, y * quadSize, (x + 1) * quadSize - 1, (y + 1) * quadSize - 1, -memoryExploreDuration);
                    }
                }
            }
            //public void NextTick(int currentTick)
            //{
            //    for (int a = 0; a < cellCount; a++)
            //    {
            //        for (int b = 0; b < cellCount; b++)
            //        {
            //            //interesQuads[a, b] = new InteresQuad(a * cellCount, b * cellCount, (a + 1) * cellCount - 1, (b + 1) * cellCount - 1, -memoryExploreDuration);

            //        }
            //    }
            //}
            public InteresQuad this[int x, int y]
            {
                get {
                    if (x >= 0 && x < cellCount && y >= 0 && y < cellCount)
                        return interesQuads[x, y];
                    else
                        throw new System.Exception("неверный индекс массива");
                }
            }
            public void CheckBorder(int x, int y)
            {

                for (int i = 0; i < 8; i++)
                {
                    int nx = x;
                    int ny = y;
                    if (i == 0) nx++;
                    if (i == 1) nx--;
                    if (i == 2) ny++;
                    if (i == 3) ny--;
                    if (i == 4) { nx++; ny++; }
                    if (i == 5) { nx--; ny++; }
                    if (i == 6) { nx++; ny--; }
                    if (i == 7) { nx--; ny--; }

                    if (nx >= 0 && nx < cellCount && ny >= 0 && ny< cellCount)
                    {
                        if (interesQuads[nx, ny]._isMyTerritory == false)
                            interesQuads[nx, ny]._isMyBorder = true;
                    }
                }

            }
        }
        InteresMap interesMap;
        //List<Vec2Int> preSetHousePositions;
        //bool preSetHousePlacingComplete = false;

        class DeadEndMap
        {
            int _mapSize;
            int[,] _map;

            public DeadEndMap(int mapSize)
            {
                _mapSize = mapSize;
                _map = new int[_mapSize, _mapSize];
            }

            public void Reset()
            {
                for (int x = 0; x < _mapSize; x++)
                {
                    for (int y = 0; y < _mapSize; y++)
                    {
                        _map[x, y] = 0;
                    }
                }
            }

            public int this[int x , int y]
            {
                get {
                    if (x >= 0 && x < _mapSize && y >= 0 && y < _mapSize)
                        return _map[x, y];
                    else
                        throw new System.Exception("Неверный индекс массива");
                }                
            }
            public void Increment(int x, int y, int value = 1)
            {
                if (x >= 0 && x < _mapSize && y >= 0 && y < _mapSize)
                    _map[x, y] += value;                
            }
            public void Emit(int x, int y, int value)
            {
                int sx = x;
                int sy = y;
                int flag = 3;   //  /2  \3
                int dx = 0;     //  \1  /0
                int dy = 0; // with my cell
                //int flag = 0;   //  /2  \3
                //int dx = 1;     //  \1  /0
                //int dy = 0; // without my cell
                for (int step = 0; step <= value;)
                {
                    //рисуем
                    int nx = sx + dx;
                    int ny = sy + dy;

                    if (nx >= 0 && nx < _mapSize && ny >= 0 && ny < _mapSize)
                        _map[nx, ny] += value - step;

                    //двигаем цель
                    if (flag == 0)
                    {
                        dx--;
                        dy--;
                        if (dx == 0) flag = 1;
                    }
                    else if (flag == 1)
                    {
                        dx--;
                        dy++;
                        if (dy == 0) flag = 2;
                    }
                    else if (flag == 2)
                    {
                        dx++;
                        dy++;
                        if (dx == 0) flag = 3;
                    }
                    else if (flag == 3)
                    {
                        dx++;
                        dy--;
                        if (dy == 0)
                        {
                            flag = 0;
                            dx++;
                            step++;
                        }
                        else if (dy < 0)// first shift from 0,0
                        {
                            dx = 1;
                            dy = 0;
                            flag = 0;
                            step++;
                        }
                    }
                }
            }
        }
        DeadEndMap deadEndMap;

        class PotencTargetCell
        {
            int[] availableTargetTypes;
            public bool CanAttackWarriors { get; private set; }
            public bool CanAttackTurret { get; private set; }
            public bool CanAttackBase { get; private set; }
            public bool CanAttackHouse { get; private set; }
            public bool CanAttackBuilder { get; private set; }
            public bool CanAttackResource { get; private set; }
            public bool CanAttackWall { get; private set; }
            public int Sum { get; private set; }

            public PotencTargetCell()
            {
                availableTargetTypes = new int[entityTypesArray.Length];
                Reset();
            }
            public void Reset()
            {
                for (int i = 0; i < availableTargetTypes.Length; i++)
                {
                    availableTargetTypes[i] = 0;
                }
                CanAttackBase = false;
                CanAttackBuilder = false;
                CanAttackHouse = false;
                CanAttackResource = false;
                CanAttackTurret = false;
                CanAttackWall = false;
                CanAttackWarriors = false;
                Sum = 0;
            }
            public int this[EntityType type]
            {
                get {return availableTargetTypes[(int)type] ; }
                set { availableTargetTypes[(int)type] = value; }
            }
            public void Check()
            {
                if (availableTargetTypes[(int)EntityType.BuilderBase] > 0
                    || availableTargetTypes[(int)EntityType.RangedBase] > 0
                    || availableTargetTypes[(int)EntityType.MeleeBase] > 0)
                    CanAttackBase = true; // 3/10

                if (availableTargetTypes[(int)EntityType.MeleeUnit] > 0
                    || availableTargetTypes[(int)EntityType.RangedUnit] > 0)
                    CanAttackWarriors = true; // 5/10

                if (availableTargetTypes[(int)EntityType.Resource] > 0)
                    CanAttackResource = true; // 6/10
                if (availableTargetTypes[(int)EntityType.House] > 0)
                    CanAttackHouse = true; // 7/10
                if (availableTargetTypes[(int)EntityType.Turret] > 0)
                    CanAttackTurret = true; // 8/10
                if (availableTargetTypes[(int)EntityType.BuilderUnit] > 0)
                    CanAttackBuilder = true; // 9/10
                if (availableTargetTypes[(int)EntityType.Wall] > 0)
                    CanAttackWall = true; // 10/10
                foreach (var c in availableTargetTypes)
                    Sum += c;
            }
        }
        class PotencTarget5Map
        {
            PotencTargetCell[,] _potencTargetMap;
            int _mapSize;

            public PotencTarget5Map(int mapS)
            {
                _mapSize = mapS;
                _potencTargetMap = new PotencTargetCell[_mapSize, _mapSize];
                for (int x = 0; x < _mapSize; x++)
                {
                    for (int y = 0; y < _mapSize; y++)
                    {
                        _potencTargetMap[x, y] = new PotencTargetCell();
                    }
                }
            }
            public PotencTargetCell this[int x, int y]
            {
                get
                {
                    if (x >= 0 && x < _mapSize && y >= 0 && y < _mapSize)
                        return _potencTargetMap[x, y];
                    else
                        throw new System.Exception("Недопустимое значение индекса массива");
                }
            }

            public void Reset()
            {
                for (int x = 0; x < _mapSize; x++)
                {
                    for (int y = 0; y < _mapSize; y++)
                    {
                        _potencTargetMap[x, y].Reset();
                    }
                }
            }
            public void Emit(int sx, int sy, EntityType entityType, int dist)
            {
                int size = properties[entityType].Size;
                dist += 1; // начинаем отсчет от своей клетки
                int sxRight = sx + size - 1;
                int syUp = sy + size - 1;
                for (int si = 0; si < size; si++)
                {
                    //my base
                    for (int siy = 0; siy < size; siy++)
                    {
                        //SetSafe(sx + si, sy + siy, entityType);
                    }

                    //straight
                    for (int di = 1; di < dist; di++)
                    {
                        IncrementSafe(sx - di, sy + si, entityType);// left
                        IncrementSafe(sx + si, sy - di, entityType);// down
                        IncrementSafe(sxRight + di, sy + si, entityType);// right
                        IncrementSafe(sx + si, syUp + di, entityType);// up
                    }
                }
                //diagonal
                for (int aa = 1; aa < dist - 1; aa++)
                {
                    for (int bb = 1; bb < dist - aa; bb++)
                    {
                        IncrementSafe(sx - aa, sy - bb, entityType);//left-down
                        IncrementSafe(sx - aa, syUp + bb, entityType);//left-up
                        IncrementSafe(sxRight + aa, syUp + bb, entityType);//right-up
                        IncrementSafe(sxRight + aa, sy - bb, entityType);//right-down
                    }
                }
            }
            public void IncrementSafe(int x, int y, EntityType entityType, int value = 1)
            {
                if (x >= 0 && x < _mapSize && y >= 0 && y < _mapSize)
                {
                    _potencTargetMap[x, y][entityType] += value;
                }
            }
            public void CheckAll()
            {
                for (int x = 0; x < _mapSize; x++)
                {
                    for (int y = 0; y < _mapSize; y++)
                    {
                        _potencTargetMap[x, y].Check();
                    }
                }
            }
        }
        PotencTarget5Map potencTarget5Map;

        struct EnemyDangerCell
        {
            public byte rangersWarning; // могут подойти в следующий ход
            public byte rangersAim; // могут атаковать сейчас
            public byte turretsAim;
            public byte meleesWarning;
            public byte meleesAim;
            public byte buildersWarning;
            public byte buildersAim;
            //public EnemyDangerCell(EnemyDangerCell cell)
            //{
            //    rangersWarning = cell.rangersAim; // могут подойти в следующий ход
            //    rangersAim = cell.rangersAim; // могут атаковать сейчас
            //    turretsAim = cell.;
            //    meleesWarning = 0;
            //    meleesAim = 0;
            //    builderWarning = 0;
            //    builderAim = 0;
            //}
        }
        EnemyDangerCell[][] enemyDangerCells;

        class AttackDistanceByType
        {
            //static int[] _potencTargetDistancsByType = new int[entityTypesArray.Length];
            public static int Get(EntityType entityType)
            {
                switch (entityType)
                {
                    case EntityType.BuilderBase: return 5; 
                    case EntityType.BuilderUnit: return 5; 
                    case EntityType.House: return 2; 
                    case EntityType.MeleeBase: return 5; 
                    case EntityType.MeleeUnit: return 5; 
                    case EntityType.RangedBase: return 5; 
                    case EntityType.RangedUnit: return 5; 
                    case EntityType.Resource: return 5; 
                    case EntityType.Turret: return 5; 
                    case EntityType.Wall: return 1; 
                    default: return 0;
                }
            }
        }
        class PotencAttackCell
        {
            public int dist5low;
            public int dist6;
            public int dist7;
            public int dist8;
            public int min;
            public bool drawn;
            public PotencAttackCell()
            {
                Reset();
            }
            public void Reset()
            {
                dist5low = dist6 = dist7 = dist8 = 0;
                drawn = false;
            }
            public void CalcMin()
            {
                min = MinWithoutZero( MinWithoutZero( MinWithoutZero(dist5low, dist6), dist7), dist8);
            }
            public bool TryDraw()
            {
                if (drawn == false)
                {
                    drawn = true;
                    return true;
                }
                return false;
            }
        }
        class PotencAttackMap
        {
            PotencAttackCell[,] _potencAttackMap;
            int _mapSize;

            public PotencAttackMap(int mapS)
            {
                _mapSize = mapS;
                _potencAttackMap = new PotencAttackCell[_mapSize, _mapSize];
                for (int x = 0; x < _mapSize; x++)
                {
                    for (int y = 0; y < _mapSize; y++)
                    {
                        _potencAttackMap[x, y] = new PotencAttackCell();
                    }
                }
            }

            public PotencAttackCell this[int x, int y]
            {
                get
                {
                    if (x >= 0 && x < _mapSize && y >= 0 && y < _mapSize)
                        return _potencAttackMap[x, y];
                    else
                        throw new System.Exception("Недопустимое значение индекса массива"); ;
                }
            }
            public void Reset()
            {
                for (int x = 0; x < _mapSize; x++)
                {
                    for (int y = 0; y < _mapSize; y++)
                    {
                        _potencAttackMap[x, y].Reset();
                    }
                }
            }
            public void AddCell(int x, int y, int d5, int d6, int d7, int d8)
            {
                if (x >= 0 && x < _mapSize && y >= 0 && y < _mapSize)
                {
                    _potencAttackMap[x, y].dist5low += d5;
                    _potencAttackMap[x, y].dist6 += d6;
                    _potencAttackMap[x, y].dist7 += d7;
                    _potencAttackMap[x, y].dist8 += d8;
                }
            }
            public void AddCell(int x, int y, int dist, bool increment)
            {
                if (x >= 0 && x < _mapSize && y >= 0 && y < _mapSize)
                {
                    if (increment)
                    {
                        if (dist <= 5) _potencAttackMap[x, y].dist5low++;
                        else if (dist == 6) _potencAttackMap[x, y].dist6++;
                        else if (dist == 7) _potencAttackMap[x, y].dist7++;
                        else if (dist == 8) _potencAttackMap[x, y].dist8++;
                    }
                    else
                    {
                        if (dist <= 5) _potencAttackMap[x, y].dist5low--;
                        else if (dist == 6) _potencAttackMap[x, y].dist6--;
                        else if (dist == 7) _potencAttackMap[x, y].dist7--;
                        else if (dist == 8) _potencAttackMap[x, y].dist8--;
                    }
                }
            }
            public bool TryDraw(int x, int y, DebugInterface debugInterface)
            {
                if (x >= 0 && x < _mapSize && y >= 0 && y < _mapSize)
                {
                    if (_potencAttackMap[x, y].TryDraw())
                    {
                        PotencAttackCell cell = _potencAttackMap[x, y];
                        int d5 = cell.dist5low;
                        int d6 = cell.dist6;
                        int d7 = cell.dist7;
                        int d8 = cell.dist8;
                        int sum = d5 + d6 + d7 + d8;
                        int min = cell.min;
                        int textSize = 16;
                        if (min != 0)
                        {
                            ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y + 0.2f), new Vec2Float(0, 0), min > 0 ? colorGreen : colorRed);
                            debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, min.ToString(), 0.5f, textSize)));
                        }
                        //if (d5 != 0)
                        //{
                        //    ColoredVertex position = new ColoredVertex(new Vec2Float(x, y + 0.5f), new Vec2Float(0, 0), colorRed);
                        //    debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, d5.ToString(), 0, textSize)));
                        //}
                        //if (d6 != 0)
                        //{
                        //    ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y + 0.5f), new Vec2Float(0, 0), colorMagenta);
                        //    debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, d6.ToString(), 0f, textSize)));
                        //}
                        //if (d7 != 0)
                        //{
                        //    ColoredVertex position = new ColoredVertex(new Vec2Float(x, y + 0.05f), new Vec2Float(0, 0), colorGreen);
                        //    debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, d7.ToString(), 0f, textSize)));
                        //}
                    }

                }
                return false;
            }
            public void CalcMin()
            {
                for (int x = 0; x < _mapSize; x++)
                {
                    for (int y = 0; y < _mapSize; y++)
                    {
                        _potencAttackMap[x, y].CalcMin();
                    }
                }
            }
        }
        PotencAttackMap potencAttackMap;

        float buildBuildingDistCoef = 0.3f; // коэффициент как далеко мы ищем строителей для помощи 0,3 = 30% от расчетного времени строительства (меньше ищем)
        float repairBuildingDistCoef = 0.5f; // аналогично но для ремонта
        int buildTurretThenResourcesOver = 720;
        Vec2Int rangedBasePotencPlace1;
        Vec2Int rangedBasePotencPlace2;

        const float ratioRangedToBuilder = 0.4f; // соотношение лучников к строителям: 10 строителей на 4 лучников

        //int builderCountForStartBuilding = 3; // количество ближайших свободных строителей которое ищется при начале строительства
        //float startBuildingFindDistanceFromHealth = 0.4f; // дистанция поиска строителей как процент здоровья 

        #region Статистические переменные
        Dictionary<Model.EntityType, int> currentMyEntityCount = new Dictionary<Model.EntityType, int>();
        Dictionary<Model.EntityType, int> previousEntityCount = new Dictionary<Model.EntityType, int>();
        Dictionary<Model.EntityType, float> buildEntityPriority = new Dictionary<Model.EntityType, float>();
        Dictionary<int, Entity> enemiesById = new Dictionary<int, Entity>();

        int howMuchResourcesIHaveNextTurn = 0;
        int nextTurnResourcesSelectCount = 5;
        int nextTurnResourcesBonus = 1;
        int howMuchResourcesCollectLastTurn = 0;
        const int howManyTurnsHistory = 30;
        int[] howMuchResourcesCollectLastNTurns = new int[howManyTurnsHistory];
        int[] howMuchResourcesCollectCPALastNTurns = new int[howManyTurnsHistory];
        int[] howMuchLiveBuildersLast10Turns = new int[howManyTurnsHistory];
        int howMuchResourcesCollectAll = 0;
        bool iHaveActiveRangedBase = false;
        bool iHaveActiveBuilderBase = false;
        bool opponentHasResourcesForRangersBase = false;
        bool isFinal = false;

        int myDeadBuilders = 0;
        int myDeadRangers = 0;
        int myDeadMelees = 0;
        int myResources;
        int myScore;
        int myId;
        int mapSize;
        int populationMax = 0;
        int populationUsing = 0;
        bool fogOfWar;

        #endregion
        #region Желания, Планы, Намерения и т.д.

        enum DesireType
        {
            WantCreateBuilders, WantCreateRangers,
            WantCreateHouses, WantCreateRangerBase, WantCreateTurret,
            WantRepairBuildings,
            WantCollectResources, WantRetreatBuilders,
            WantTurretAttacks, WantAllMeleesAttack
        };
        List<DesireType> desires = new List<DesireType>();
        List<DesireType> prevDesires = new List<DesireType>();

        enum PlanType
        {
            PlanCreateBuilders, PlanCreateRangers,
            PlanCreateHouses, PlanCreateRangerBase, PlanCreateTurret,
            PlanRepairNewBuildings, PlanRepairOldBuildings,
            PlanExtractResources, PlanRetreatBuilders,
            PlanTurretAttacks, PlanAllMeleesAttack
        }
        List<PlanType> plans = new List<PlanType>();
        List<PlanType> prevPlans = new List<PlanType>();

        enum IntentionType
        {
            IntentionCreateBuilder, IntentionStopCreatingBuilder,
            IntentionCreateRanger, IntentionStopCreatingRanger,
            IntentionCreateHouse, IntentionCreateRangedBase, IntentionCreateTurret,
            IntentionRepairNewBuilding, IntentionRepairOldBuilding,
            IntentionExtractResources, IntentionFindResources, IntentionRetreatBuilders, IntentionMyBuiAttackEnemyBui,
            IntentionTurretAttacks,
            IntentionAllWarriorsAttack
        }
        class Intention
        {
            public IntentionType intentionType;
            public int targetId;
            public Group targetGroup;
            public Vec2Int targetPosition;
            public EntityType targetEntityType;

            public Intention(IntentionType type, Vec2Int pos, EntityType _entityType)
            {
                intentionType = type;
                targetId = -1;
                targetGroup = new Group();
                targetPosition = pos;
                targetEntityType = _entityType;
            }
            public Intention(IntentionType type, int _targetId)
            {
                intentionType = type;
                targetId = _targetId;
                targetGroup = new Group();
                targetPosition = new Vec2Int();
                targetEntityType = EntityType.Resource;
            }
            public Intention(IntentionType type, Group _targetGroup)
            {
                intentionType = type;
                targetId = -1;
                targetGroup = _targetGroup;
                targetPosition = new Vec2Int();
                targetEntityType = EntityType.Resource;
            }
        }
        List<Intention> intentions = new List<Intention>();
        List<Intention> prevIntentions = new List<Intention>();

        Dictionary<int, EntityAction> actions = new Dictionary<int, EntityAction>();
        #endregion

        struct DebugLine
        {
            public float _x1;
            public float _x2;
            public float _y1;
            public float _y2;
            public Color _color1;
            public Color _color2;
            public DebugLine(float x1, float y1, float x2, float y2, Color color1, Color color2)
            {
                _x1 = x1;
                _y1 = y1;
                _x2 = x2;
                _y2 = y2;
                _color1 = color1;
                _color2 = color2;
            }
        }
        List<DebugLine> debugLines = new List<DebugLine>();

        public Action GetAction(PlayerView playerView, DebugInterface debugInterface)
        {
            _playerView = playerView;
            _debugInterface = debugInterface;

            #region first initialization arrays and lists (once)
            if (needPrepare == true)
            {
                //once prepare variables and struct
                myId = _playerView.MyId;
                mapSize = _playerView.MapSize;
                properties = _playerView.EntityProperties;
                needPrepare = false;
                fogOfWar = _playerView.FogOfWar;
                if (_playerView.Players.Length == 2)
                {
                    isFinal = true;
                }
                Prepare();

            }

            #endregion

            #region calc statistics and informations
            foreach (var p in _playerView.Players)
            {
                if (p.Id == myId)
                {
                    howMuchResourcesCollectLastTurn = p.Resource - myResources; // не учитывает стоимость произведенных сущностей
                    myResources = p.Resource;
                    myScore = p.Score;
                    break;
                }
            }
            CountNumberOfEntitiesAndMap();
            CheckAliveAndDieEntities();
            CheckDeadResourceOnCurrentVisibleMap();
            GenerateDeadEndMap();
            
            GenerateBuildBarrierMap();
            GeneratePotencAttackMap();
            GeneratePotencTargetMap();
            SelectRangedBasePotencPlace();
            UpdateInteresMap();

            iHaveActiveRangedBase = false;
            foreach (var id in basicEntityIdGroups[EntityType.RangedBase].members)
            {
                if (entityMemories[id].myEntity.Active == true)
                {
                    iHaveActiveRangedBase = true;
                    break;
                }
            }
            iHaveActiveBuilderBase = false;
            foreach (var id in basicEntityIdGroups[EntityType.BuilderBase].members)
            {
                if (entityMemories[id].myEntity.Active == true)
                {
                    iHaveActiveBuilderBase = true;
                    break;
                }
            }


            #endregion

            debugLines.Clear();

            GenerateDesires(); // Желания - Что я хочу сделать?       
            ConvertDesiresToPlans(); // Планы - Какие из желаний я могу сейчас сделать?

            PrepareBeforeCreateIntentions(); //Подготовительные меропрития перед созданиями намерений
            ConvertPlansToIntentions(); // Намерения - Как и кем я буду выполнять планы?
            CorrectCrossIntentions();// Проверяем взаимоискулючающие и противоречащие намерения. Оставляем только нужные.

            ConvertIntentionsToOrders(); // определяем конкретные приказы для каждой сущности
            OptimizeOrders(); // корректируем приказы, чтобы не было столкновений

            ConvertOrdersToActions(); // Приказы - Кто будет выполнять намерения? //приказы превращаются в конкретные action для entities        

            SaveEntitiesMemory();

            if (debugOptions[(int)DebugOptions.canDrawGetAction])
            {
                if (debugOptions[(int)DebugOptions.drawPotencAttackOverMy] == true)
                {
                    DrawPotencMapOverMyUnits(3);
                }
                if (debugOptions[(int)DebugOptions.drawPotencAttackAll] == true)
                {
                    DrawPotencMapAll();
                }
                if (debugOptions[(int)DebugOptions.drawOnceVisibleMap] == true)
                {
                    for (int x = 0; x < mapSize; x++)
                    {
                        for (int y = 0; y < mapSize; y++)
                        {
                            ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y + 0.3f), new Vec2Float(0, 0), colorRed);
                            debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, onceVisibleMap[x][y].ToString(), 0, 16)));
                            //if (onceVisibleMap[x][y] == 0)
                            //{
                            //    ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y+0.3f), new Vec2Float(0, 0), colorRed);
                            //    debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, "x", 0, 16)));
                            //}
                        }
                    }
                }
                if (debugOptions[(int)DebugOptions.drawRangedBasePotencPlace] == true)
                {
                    int x1 = rangedBasePotencPlace1.X - 1;
                    int y1 = rangedBasePotencPlace1.Y - 1;
                    int size = 7;
                    DrawLineOnce(x1, y1, x1 + size, y1, colorWhite,colorWhite);
                    DrawLineOnce(x1 + size, y1, x1 + size, y1 + size, colorWhite, colorWhite);
                    DrawLineOnce(x1 + size, y1 + size, x1, y1 + size, colorWhite, colorWhite);
                    DrawLineOnce(x1, y1 + size, x1, y1, colorWhite, colorWhite);

                    x1 = rangedBasePotencPlace2.X - 1;
                    y1 = rangedBasePotencPlace2.Y - 1;
                    size = 7;
                    DrawLineOnce(x1, y1, x1 + size, y1, colorGreen, colorGreen);
                    DrawLineOnce(x1 + size, y1, x1 + size, y1 + size, colorGreen, colorGreen);
                    DrawLineOnce(x1 + size, y1 + size, x1, y1 + size, colorGreen, colorGreen);
                    DrawLineOnce(x1, y1 + size, x1, y1, colorGreen, colorGreen);

                    //for (int x = 0; x < mapSize; x++)
                    //{
                    //    for (int y = 0; y < mapSize; y++)
                    //    {
                    //        //int res = buildBarrierMap[x, y].HowManyResBarrier(5);
                    //        //if (res > 0)
                    //        //{

                    //        //    ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y + 0.3f), new Vec2Float(0, 0), colorRed);
                    //        //    debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, res.ToString(), 0.5f, 16)));
                    //        //}
                           
                    //        if (buildBarrierMap[x, y].s5noUnvisivleBarrier == false)
                    //        {

                    //            ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y + 0.3f), new Vec2Float(0, 0), colorRed);
                    //            debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, "x", 0.5f, 16)));
                    //        }
                    //    }
                    //}
                }
                if (debugOptions[(int)DebugOptions.drawInteresMap] == true)
                {
                    DrawInteresMap();
                }
                if (debugOptions[(int)DebugOptions.drawMemoryEnemies] == true)
                {
                    DrawMemoryEnemies();
                }
                if (debugOptions[(int)DebugOptions.drawMemoryResources] == true)
                {
                    DrawMemoryResources();
                }
                if (debugOptions[(int)DebugOptions.drawDeadCellMap] == true)
                {
                    DrawDeadCellMap();
                }
                if (debugOptions[(int)DebugOptions.drawPotencTarget5Map] == true)
                {
                    DrawPotencTarget5Map();
                }
                if (debugOptions[(int)DebugOptions.drawDeadStatistic] == true)
                {
                    DrawDeadStatistic();
                }
                
                _debugInterface.Send(new DebugCommand.Flush());
            }

            return new Action(actions);
        }

        void Prepare()
        {
            #region draw debug settings
            debugOptions[(int)DebugOptions.canDrawGetAction] = true;
            debugOptions[(int)DebugOptions.drawRetreat] = true;
            debugOptions[(int)DebugOptions.drawBuildBarrierMap] = false;
            debugOptions[(int)DebugOptions.drawOnceVisibleMap] = false;
            debugOptions[(int)DebugOptions.drawInteresMap] = false;
            debugOptions[(int)DebugOptions.drawMemoryResources] = false;
            debugOptions[(int)DebugOptions.drawMemoryEnemies] = true;
            debugOptions[(int)DebugOptions.drawOrderStatistics] = true;
            debugOptions[(int)DebugOptions.drawDeadCellMap] = false;
            debugOptions[(int)DebugOptions.drawPlanedKill] = false;
            debugOptions[(int)DebugOptions.drawOptRangerMove] = true;
            debugOptions[(int)DebugOptions.drawOptRangerPathfind] = false;
            debugOptions[(int)DebugOptions.drawDeadStatistic] = true;

            debugOptions[(int)DebugOptions.drawBuildAndRepairOrder] = true;
            debugOptions[(int)DebugOptions.drawBuildAndRepairPath] = false;
            debugOptions[(int)DebugOptions.drawPotencAttackOverMy] = false;
            debugOptions[(int)DebugOptions.drawPotencAttackAll] = false;
            debugOptions[(int)DebugOptions.drawPotencAttackMove] = true;
            debugOptions[(int)DebugOptions.drawPotencAttackPathfind] = false;
            debugOptions[(int)DebugOptions.drawPotencTarget5Map] = false;
            debugOptions[(int)DebugOptions.drawRangedBasePotencPlace] = true;
            debugOptions[(int)DebugOptions.drawOptAttack] = true;

            debugOptions[(int)DebugOptions.canDrawDebugUpdate] = false;

            if (_debugInterface == null)
            {
                debugOptions[(int)DebugOptions.canDrawGetAction] = false;
                debugOptions[(int)DebugOptions.canDrawDebugUpdate] = false; // отображение отладочной информации на стадии debugUpdate
            }
            else
            {
                _debugInterface.Send(new DebugCommand.Clear());
                _debugInterface.Send(new DebugCommand.SetAutoFlush(false));
            }
            #endregion

            #region init arrays
            cellWithIdAny = new int[mapSize][];
            cellWithIdOnlyBuilding = new int[mapSize][];
            onceVisibleMap = new int[mapSize][];
            currentVisibleMap = new bool[mapSize][];
            resourceMemoryMap = new int[mapSize][];
            enemyDangerCells = new EnemyDangerCell[mapSize][];
            resourcePotentialField = new int[mapSize][];
            nextPositionMyUnitsMap = new int[mapSize][];

            for (var i = 0; i < mapSize; i++)
            {
                cellWithIdAny[i] = new int[mapSize];
                cellWithIdOnlyBuilding[i] = new int[mapSize];
                onceVisibleMap[i] = new int[mapSize];
                currentVisibleMap[i] = new bool[mapSize];
                resourceMemoryMap[i] = new int[mapSize];
                enemyDangerCells[i] = new EnemyDangerCell[mapSize];
                resourcePotentialField[i] = new int[mapSize];
                nextPositionMyUnitsMap[i] = new int[mapSize];
                // auto zeroing all when created
            }

            deadEndMap = new DeadEndMap(mapSize);
            potencAttackMap = new PotencAttackMap(mapSize);
            buildBarrierMap = new BuildBarrierMap(mapSize);
            interesMap = new InteresMap(mapSize);
            potencTarget5Map = new PotencTarget5Map(mapSize);      

            #endregion

            if (!fogOfWar)
            {
                for (int x = 0; x < mapSize; x++)
                {
                    for (int y = 0; y < mapSize; y++)
                    {
                        onceVisibleMap[x][y] = 20;
                        currentVisibleMap[x][y] = true;
                    }
                }
            }

            //prepare current and previous entity counter
            foreach (var ent in entityTypesArray)
            {
                currentMyEntityCount.Add(ent, 0);
                previousEntityCount.Add(ent, 0);
                buildEntityPriority.Add(ent, 0f);
                basicEntityIdGroups.Add(ent, new Group());
            }


        }
        void CheckAliveAndDieEntities()
        {
            needRepairBuildingIdList.Clear();
            needRepairUnitsIdList.Clear();

            int currentTick = _playerView.CurrentTick;

            //zeroing visible map all only with for of war
            if (fogOfWar)
            {
                for (int x = 0; x < mapSize; x++)
                    currentVisibleMap[x] = new bool[mapSize];
            }
            // zero enemies dictionary
            //enemiesById.Clear();
            // zero enemy danger cells
            for (var x = 0; x < mapSize; x++)
            {
                for (var y = 0; y < mapSize; y++)
                {
                    enemyDangerCells[x][y] = new EnemyDangerCell();
                }
            }

            //uncheck memory
            foreach (var m in entityMemories)
            {
                m.Value.checkedNow = false;
            }
            //update and add live entity
            foreach (var e in _playerView.Entities)
            {
                //use only my entities
                if (e.PlayerId == myId)
                {
                    if (entityMemories.ContainsKey(e.Id))
                    {
                        //update
                        entityMemories[e.Id].Update(e);
                        //once and current visible map update
                        AddEntityViewToOnceVisibleMap(e.EntityType, e.Position.X, e.Position.Y);
                        AddEntityViewToCurrentVisibleMap(e.EntityType, e.Position.X, e.Position.Y);
                    }
                    else
                    {
                        //add my entity
                        var em = new EntityMemory(e);
                        em.SetGroup(basicEntityIdGroups[e.EntityType]);
                        entityMemories.Add(e.Id, em);
                        //once and current visible map update
                        AddEntityViewToOnceVisibleMap(e.EntityType, e.Position.X, e.Position.Y);
                        AddEntityViewToCurrentVisibleMap(e.EntityType, e.Position.X, e.Position.Y);

                        howMuchResourcesCollectLastTurn += properties[e.EntityType].InitialCost + currentMyEntityCount[e.EntityType] - 1;
                    }
                    // добаввляем раненых в списки лечения
                    if (e.Health < properties[e.EntityType].MaxHealth)
                    {
                        if (properties[e.EntityType].CanMove)
                            needRepairUnitsIdList.Add(e.Id);
                        else
                            needRepairBuildingIdList.Add(e.Id);
                    }
                }
                else if (e.PlayerId == null)// it s resource
                {
                    int x = e.Position.X;
                    int y = e.Position.Y;

                    resourceMemoryMap[x][y] = currentTick + 1;

                    if (fogOfWar)
                    {
                        if (isFinal) // final
                        {
                            int nx = mapSize - 1 - x; // diagonal
                            int ny = mapSize - 1 - y;
                            if (onceVisibleMap[nx][ny] == 0)
                            {
                                resourceMemoryMap[nx][ny] = currentTick + 1;
                            }
                        } else // round 2
                        {
                            int nx = mapSize - 1 - x; // diagonal
                            int ny = mapSize - 1 - y;
                            if (onceVisibleMap[nx][ny] == 0)
                            {
                                resourceMemoryMap[nx][ny] = currentTick + 1;
                            }
                            nx = y; // Up corner
                            ny = mapSize - 1 - x;
                            if (onceVisibleMap[nx][ny] == 0)
                            {
                                resourceMemoryMap[nx][ny] = currentTick + 1;
                            }
                            nx = mapSize - 1 - y; // right corner
                            ny = x;
                            if (onceVisibleMap[nx][ny] == 0)
                            {
                                resourceMemoryMap[nx][ny] = currentTick + 1;
                            }
                        }
                    }

                }
                else // it's enemy
                {
                    if (enemiesById.ContainsKey(e.Id))
                        enemiesById[e.Id] = e;
                    else 
                        enemiesById.Add(e.Id, e);
                    AddEnemyDangerCells(e.Position.X, e.Position.Y, e.EntityType);
                }
            }
            #region удаляем мертвые сущности
            //remove my died entity
            foreach (var m in entityMemories)
            {
                if (m.Value.checkedNow == false)
                {
                    switch (m.Value.myEntity.EntityType)
                    {
                        case EntityType.BuilderUnit: myDeadBuilders++; break;
                        case EntityType.MeleeUnit: myDeadMelees++; break;
                        case EntityType.RangedUnit: myDeadRangers++; break;
                    }

                    m.Value.Die();
                    entityMemories.Remove(m.Key);
                }
            }

            //remove enemy died entity          
            List<int> deleteIds = new List<int>();
            foreach (var en in enemiesById)
            {
                int x = en.Value.Position.X;
                int y = en.Value.Position.Y;
                if (currentVisibleMap[x][y] == true)
                {
                    if (cellWithIdAny[x][y] != en.Key)
                    {
                        deleteIds.Add(en.Key);
                    }
                }
            }
            foreach(int id in deleteIds)
            {
                enemiesById.Remove(id);
            }
            #endregion

            //statistics
            if (_playerView.CurrentTick == 0)
            {
                howMuchResourcesCollectLastTurn = 0;
            }
            howMuchResourcesCollectAll += howMuchResourcesCollectLastTurn;
            for (int i = howMuchResourcesCollectCPALastNTurns.Length - 1; i > 0; i--)
            {
                howMuchResourcesCollectLastNTurns[i] = howMuchResourcesCollectLastNTurns[i - 1];
                howMuchResourcesCollectCPALastNTurns[i] = howMuchResourcesCollectCPALastNTurns[i - 1];
                howMuchLiveBuildersLast10Turns[i] = howMuchLiveBuildersLast10Turns[i - 1];
            }
            howMuchResourcesCollectLastNTurns[0] = howMuchResourcesCollectLastTurn;
            int co = currentMyEntityCount[EntityType.BuilderUnit];
            howMuchLiveBuildersLast10Turns[0] = co;
            howMuchResourcesCollectCPALastNTurns[0] = (co == 0) ? 0 : howMuchResourcesCollectLastTurn * 100 / co;

            int sum = 0;
            for (int i = 0; i < nextTurnResourcesSelectCount; i++)
            {
                sum += howMuchResourcesCollectLastNTurns[0];
            }
            howMuchResourcesIHaveNextTurn = myResources + sum / nextTurnResourcesSelectCount + nextTurnResourcesBonus;

        }
        void CheckDeadResourceOnCurrentVisibleMap()
        {
            int currentTick = _playerView.CurrentTick;
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    if (currentVisibleMap[x][y] == true)
                    {
                        if (resourceMemoryMap[x][y] > 0 && resourceMemoryMap[x][y] <= currentTick)
                            resourceMemoryMap[x][y] = 0;
                    }
                }
            }
        }
        void GenerateDeadEndMap()
        {
            deadEndMap.Reset();

            int defaultEmit = 3;
            //граница
            for (int i = 0; i < mapSize; i++)
            {
                deadEndMap.Emit(i, 0, defaultEmit);
                deadEndMap.Emit(0, i, defaultEmit);
                deadEndMap.Emit(i, mapSize - 1, defaultEmit);
                deadEndMap.Emit(mapSize - 1, i, defaultEmit);
            }
            //здания и ресурсы
            foreach(var en in _playerView.Entities)
            {
                if (properties[ en.EntityType].CanMove == false)
                {
                    int x = en.Position.X;
                    int y = en.Position.Y;
                    int size = properties[en.EntityType].Size;
                    if (size == 1)
                    {
                        deadEndMap.Emit(x, y, defaultEmit);
                    }
                    else
                    {
                        for (int i = 0; i < size; i++) // emit outside
                        {
                            deadEndMap.Emit(x - 1, y + i, defaultEmit);
                            deadEndMap.Emit(x + i, y - 1, defaultEmit);
                            deadEndMap.Emit(x + size, y + i, defaultEmit);
                            deadEndMap.Emit(x + i, y + size, defaultEmit);
                        }
                        //for (int i = 1; i < size; i++) // emit inside
                        //{
                        //    deadEndMap.Emit(x, y + i, defaultEmit);
                        //    deadEndMap.Emit(x + i - 1, y, defaultEmit);
                        //    deadEndMap.Emit(x + size - 1, y + size - 1 - i, defaultEmit);
                        //    deadEndMap.Emit(x + i, y + size - 1, defaultEmit);
                        //}
                    }
                }
            }
        }
        void GenerateResourcePotentialField()
        {
            //zeroing
            for (int i = 0; i < mapSize; i++)
            {
                resourcePotentialField[i] = new int[mapSize];
            }

            //стартовое значение, которое будем уменьшать
            int startWeight = mapSize * mapSize;
            //int firstCellWeight = startWeight - 1; // соседние клетки с ресурсом. Считаем, что строители на них всегда добывают ресурс и не строим путь через него
            //int minIndex = startIndex - maxDistance; //минимальное значение, дальше которого не будем искать            

            //добавляем стартовые клетки поиска
            List<XYWeight> findCells = new List<XYWeight>();
            foreach (var en in _playerView.Entities)
            {
                if (en.EntityType == EntityType.Resource) // it is resource
                {
                    findCells.Add(new XYWeight(en.Position.X, en.Position.Y, startWeight));
                    resourcePotentialField[en.Position.X][en.Position.Y] = startWeight;
                }
            }

            while (findCells.Count > 0)
            {
                int bx = findCells[0].x;
                int by = findCells[0].y;
                int w = findCells[0].weight;

                for (int jj = 0; jj < 4; jj++)
                {
                    int nx = bx;
                    int ny = by;
                    if (jj == 0) nx--;
                    if (jj == 1) ny--;
                    if (jj == 2) nx++;
                    if (jj == 3) ny++;

                    if (nx >= 0 && nx < mapSize && ny >= 0 && ny < mapSize)
                    {
                        if (resourcePotentialField[nx][ny] == 0)
                        {
                            bool canContinueField = true;

                            // проверка опасной зоны
                            var dCell = enemyDangerCells[nx][ny];
                            if (dCell.meleesAim + dCell.rangersAim + dCell.turretsAim > 0)
                            {
                                canContinueField = false;
                                resourcePotentialField[nx][ny] = RPFdangerCellWeight;
                            }
                            else if (dCell.meleesWarning + dCell.rangersWarning > 0)
                            {
                                canContinueField = false;
                                resourcePotentialField[nx][ny] = RPFwarningCellWeight;
                                //findCells.Add(new XYWeight(nx, ny, RPFwarningCellWeight));
                            } else if (nextPositionMyUnitsMap[nx][ny] > 0)
                            {
                                canContinueField = false;
                                resourcePotentialField[nx][ny] = RPFdeniedUnitWeight;
                            }

                            // проверка занятой клетки
                            if (canContinueField == true)
                            {
                                int id = cellWithIdAny[nx][ny];
                                if (id >= 0)// occupied cell
                                {
                                    if (entityMemories.ContainsKey(id))
                                    {
                                        if (entityMemories[id].myEntity.EntityType == EntityType.BuilderUnit)
                                        {
                                            if (w == startWeight)//check my builder на соседней клетке с ресурсомs
                                            {
                                                canContinueField = false;
                                                resourcePotentialField[nx][ny] = RPFdeniedBuilderWeight;
                                            }
                                        }
                                        else
                                        {
                                            if (properties[entityMemories[id].myEntity.EntityType].CanMove == false)//is my building
                                            {
                                                canContinueField = false;
                                                resourcePotentialField[nx][ny] = RPFmyBuildingWeight;
                                            }
                                        }
                                    }
                                    else // enemy 
                                    {
                                        if (enemiesById.ContainsKey(id))
                                        {
                                            canContinueField = false;
                                            resourcePotentialField[nx][ny] = RPFenemyEntityWeight;
                                        }
                                    }
                                }
                            }

                            if (canContinueField == true) // empty, safe cell or through free unit
                            {
                                //add weight and findCell
                                resourcePotentialField[nx][ny] = w - 1;
                                findCells.Add(new XYWeight(nx, ny, w - 1));
                            }
                        }
                        //можем не проверять уже занятые клетки, так как у нас волны распространяются по очереди 1-2-3-4 и т.д.
                    }
                }
                findCells.RemoveAt(0);
            }
        }
        void GenerateBuildBarrierMap()
        {
            //zeroing
            buildBarrierMap.Reset();

            // check entity self-place barriers
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    int id = cellWithIdAny[x][y];
                    if (id >= 0)
                    {
                        if (entityMemories.ContainsKey(id))
                        {
                            if (entityMemories[id].myEntity.EntityType == EntityType.BuilderUnit)
                            {
                                buildBarrierMap.BlockCell(x, y, BuildBarrierMap.BlockVariant.Builder);
                            }
                            else
                            {
                                buildBarrierMap.BlockCell(x, y, BuildBarrierMap.BlockVariant.MyBuilding);
                            }

                        }
                        else if (enemiesById.ContainsKey(id))
                        {
                            buildBarrierMap.BlockCell(x, y, BuildBarrierMap.BlockVariant.Enemy);

                        }
                        else // it's resource
                        {
                            buildBarrierMap.AddResource(x, y);
                        }
                    } else if (resourceMemoryMap[x][y] > 0)
                    {
                        buildBarrierMap.AddResource(x, y);
                    }
                }
            }

            // check enemy danger zone place
            foreach (var p in enemiesById)
            {
                int dangerRadius = 0;
                if (p.Value.EntityType == EntityType.Turret) dangerRadius = 6;
                else if (p.Value.EntityType == EntityType.MeleeUnit) dangerRadius = 10;
                else if (p.Value.EntityType == EntityType.RangedUnit) dangerRadius = 13;

                if (dangerRadius > 0)
                {
                    int sx = p.Value.Position.X;
                    int sy = p.Value.Position.Y;
                    // int flag = 3;   //  /2  \3
                    // int dx = 0;     //  \1  /0
                    // int dy = 0; // with my cell
                    int flag = 0;   //  /2  \3
                    int dx = 1;     //  \1  /0
                    int dy = 0; // without my cell
                    for (int step = 0; step <= dangerRadius;)
                    {
                        // отмечаем
                        int nx = sx + dx;
                        int ny = sy + dy;
                        if (nx >= 0 && nx < mapSize && ny >= 0 && ny < mapSize)
                        {
                            buildBarrierMap.BlockCell(nx, ny, BuildBarrierMap.BlockVariant.Enemy);
                        }

                        //двигаем цель
                        if (flag == 0)
                        {
                            dx--;
                            dy--;
                            if (dx == 0) flag = 1;
                        }
                        else if (flag == 1)
                        {
                            dx--;
                            dy++;
                            if (dy == 0) flag = 2;
                        }
                        else if (flag == 2)
                        {
                            dx++;
                            dy++;
                            if (dx == 0) flag = 3;
                        }
                        else if (flag == 3)
                        {
                            dx++;
                            dy--;
                            if (dy == 0)
                            {
                                flag = 0;
                                dx++;
                                step++;
                            }
                            else if (dy < 0)// first shift from 0,0
                            {
                                dx = 1;
                                dy = 0;
                                flag = 0;
                                step++;
                            }

                        }
                    }
                }
            }

            #region check base and turret border cells
            EntityType entityType = EntityType.BuilderBase;
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                int sx = entityMemories[id].myEntity.Position.X;
                int sy = entityMemories[id].myEntity.Position.Y;
                int size = properties[entityType].Size;
                for (int i = 0; i <= size; i++)
                {
                    buildBarrierMap.BlockCell(sx + size, sy + i, BuildBarrierMap.BlockVariant.MyBuilding); // right
                    buildBarrierMap.BlockCell(sx + i, sy + size, BuildBarrierMap.BlockVariant.MyBuilding); // up                  
                }
            }
            entityType = EntityType.RangedBase;
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                int sx = entityMemories[id].myEntity.Position.X;
                int sy = entityMemories[id].myEntity.Position.Y;
                int size = properties[entityType].Size;
                for (int i = 0; i <= size; i++)
                {
                    buildBarrierMap.BlockCell(sx - 1, sy + i, BuildBarrierMap.BlockVariant.MyBuilding); // left
                    buildBarrierMap.BlockCell(sx + i - 1, sy - 1, BuildBarrierMap.BlockVariant.MyBuilding); // down
                    buildBarrierMap.BlockCell(sx + size, sy + i - 1, BuildBarrierMap.BlockVariant.MyBuilding); // right
                    buildBarrierMap.BlockCell(sx + i, sy + size, BuildBarrierMap.BlockVariant.MyBuilding); // up                  
                }
            }
            entityType = EntityType.MeleeBase;
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                int sx = entityMemories[id].myEntity.Position.X;
                int sy = entityMemories[id].myEntity.Position.Y;
                int size = properties[entityType].Size;
                for (int i = 0; i <= size; i++)
                {
                    buildBarrierMap.BlockCell(sx - 1, sy + i, BuildBarrierMap.BlockVariant.MyBuilding); // left
                    buildBarrierMap.BlockCell(sx + i - 1, sy - 1, BuildBarrierMap.BlockVariant.MyBuilding); // down
                    buildBarrierMap.BlockCell(sx + size, sy + i - 1, BuildBarrierMap.BlockVariant.MyBuilding); // right
                    buildBarrierMap.BlockCell(sx + i, sy + size, BuildBarrierMap.BlockVariant.MyBuilding); // up                  
                }
            }
            entityType = EntityType.Turret;
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                int sx = entityMemories[id].myEntity.Position.X;
                int sy = entityMemories[id].myEntity.Position.Y;
                int size = properties[entityType].Size;
                for (int i = 0; i <= size; i++)
                {
                    buildBarrierMap.BlockCell(sx - 1, sy + i, BuildBarrierMap.BlockVariant.MyBuilding); // left
                    buildBarrierMap.BlockCell(sx + i - 1, sy - 1, BuildBarrierMap.BlockVariant.MyBuilding); // down
                    buildBarrierMap.BlockCell(sx + size, sy + i - 1, BuildBarrierMap.BlockVariant.MyBuilding); // right
                    buildBarrierMap.BlockCell(sx + i, sy + size, BuildBarrierMap.BlockVariant.MyBuilding); // up                  
                }
            }
            #endregion
            #region check house border cells
            entityType = EntityType.House;
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                int sx = entityMemories[id].myEntity.Position.X;
                int sy = entityMemories[id].myEntity.Position.Y;
                int size = properties[entityType].Size;

                if (sx == 0 && sy == 0)
                {
                    buildBarrierMap.BlockCell(sx + size, sy + size, BuildBarrierMap.BlockVariant.MyBuilding); // up-right corner only
                }
                else if (sx == 0)
                {
                    for (int i = -1; i <= size; i++)
                    {
                        buildBarrierMap.BlockCell(sx + size, sy + i, BuildBarrierMap.BlockVariant.MyBuilding); // right                
                    }
                }
                else if (sy == 0)
                {
                    for (int i = -1; i <= size; i++)
                    {
                        buildBarrierMap.BlockCell(sx + i, sy + size, BuildBarrierMap.BlockVariant.MyBuilding); // up         
                    }
                }
                else if (sx == 2 && sy == 2)
                {
                    for (int i = -1; i <= size; i++)
                    {
                        buildBarrierMap.BlockCell(sx - 1, sy + i, BuildBarrierMap.BlockVariant.MyBuilding); // left
                        buildBarrierMap.BlockCell(sx + i, sy - 1, BuildBarrierMap.BlockVariant.MyBuilding); // down               
                    }
                }
                else if (sx == 2 && sy > 2 && sy <= 6)
                {
                    for (int i = -1; i <= size; i++)
                    {
                        buildBarrierMap.BlockCell(sx - 1, sy + i, BuildBarrierMap.BlockVariant.MyBuilding); // left              
                    }
                }
                else if (sx == 2 && sy > 6 && sy <= 10)
                {
                    for (int i = 0; i <= size; i++)
                    {
                        buildBarrierMap.BlockCell(sx - 1, sy + i - 1, BuildBarrierMap.BlockVariant.MyBuilding); // left
                        //buildBarrierMap.BlockCell(sx + i - 1, sy - 1, false, true); // down
                        buildBarrierMap.BlockCell(sx + size, sy + i, BuildBarrierMap.BlockVariant.MyBuilding); // right
                        buildBarrierMap.BlockCell(sx + i - 1, sy + size, BuildBarrierMap.BlockVariant.MyBuilding); // up                  
                    }
                }
                else if (sy == 2 && sx > 2 && sx <= 6)
                {
                    for (int i = -1; i <= size; i++)
                    {
                        buildBarrierMap.BlockCell(sx + i, sy - 1, BuildBarrierMap.BlockVariant.MyBuilding); // down              
                    }
                }
                else if (sy == 2 && sx > 6 && sx <= 10)
                {
                    for (int i = -1; i <= size; i++)
                    {
                        //buildBarrierMap.BlockCell(sx - 1, sy + i, false, true); // left
                        buildBarrierMap.BlockCell(sx + i - 1, sy - 1, BuildBarrierMap.BlockVariant.MyBuilding); // down
                        buildBarrierMap.BlockCell(sx + size, sy + i - 1, BuildBarrierMap.BlockVariant.MyBuilding); // right
                        buildBarrierMap.BlockCell(sx + i, sy + size, BuildBarrierMap.BlockVariant.MyBuilding); // up                
                    }
                }
                else
                {
                    for (int i = 0; i <= size; i++)
                    {
                        buildBarrierMap.BlockCell(sx - 1, sy + i, BuildBarrierMap.BlockVariant.MyBuilding); // left
                        buildBarrierMap.BlockCell(sx + i - 1, sy - 1, BuildBarrierMap.BlockVariant.MyBuilding); // down
                        buildBarrierMap.BlockCell(sx + size, sy + i - 1, BuildBarrierMap.BlockVariant.MyBuilding); // right
                        buildBarrierMap.BlockCell(sx + i, sy + size, BuildBarrierMap.BlockVariant.MyBuilding); // up                  
                    }
                }
            }
            #endregion

            #region блокировать строительство базы рейнджеров в левом углу
            for (int i = 0; i < 12; i++)
            {
                buildBarrierMap[0, i].s5noBaseOrWarriorBarrier = false;
                buildBarrierMap[i, 0].s5noBaseOrWarriorBarrier = false;
            }
            #endregion

            #region uncheck fog of war
            if (fogOfWar)
            {
                for (int x = mapSize - 1; x > 0; x--)
                {
                    for (int y = mapSize - 1; y > 0; y--)
                    {
                        if (onceVisibleMap[x][y] == 0)
                        {
                            if (onceVisibleMap[x - 1][y] != 0 || onceVisibleMap[x][y - 1] != 0)
                                buildBarrierMap.BlockCell(x, y, BuildBarrierMap.BlockVariant.Unvisible);
                        }
                    }
                }
            }
            #endregion
            // calc can build now
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    BuildMapCell cell = buildBarrierMap[x, y];
                    if (fogOfWar == true)
                    {
                        if (onceVisibleMap[x][y] > 0)
                        {
                            cell.Check();
                        }
                    }
                    else
                    {
                        cell.Check();
                    }
                }
            }

            if (debugOptions[(int)DebugOptions.canDrawGetAction] && debugOptions[(int)DebugOptions.drawBuildBarrierMap])
            {
                for (int x = 0; x < mapSize; x++)
                {
                    for (int y = 0; y < mapSize; y++)
                    {
                        if (buildBarrierMap[x, y].s5canBuildAfter == true)
                        {
                            ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y + 0.3f), new Vec2Float(0, 0),
                                (buildBarrierMap[x, y].s5canBuildNow) ? colorBlue : colorMagenta);
                            _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, "5", 0.5f, 16)));
                        }
                        else if (buildBarrierMap[x, y].s3canBuildAfter == true)
                        {
                            ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y + 0.3f), new Vec2Float(0, 0),
                                (buildBarrierMap[x, y].s3canBuildNow) ? colorBlue : colorMagenta);
                            _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, "3", 0.5f, 16)));
                        }
                        // don't show 2 (turret) place
                        //else if (buildBarrierMap[x, y].s2canBuildAfter == true)
                        //{
                        //    ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y + 0.3f), new Vec2Float(0, 0),
                        //        (buildBarrierMap[x, y].s2canBuildNow) ? colorBlue : colorMagenta);
                        //    _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, "2", 0.5f, 16)));
                        //}
                    }
                }
                #region примеры использования
                //if (playerView.CurrentTick == 10)
                //{
                //    debugInterface.Send(new DebugCommand.Add(new DebugData.Log("Тестовое сообщение")));

                //    ColoredVertex position = new ColoredVertex(null, new Vec2Float(10, 10), colorGreen);
                //    DebugData.PlacedText text = new DebugData.PlacedText(position, "Ghbdtn", 0, 16);
                //    debugInterface.Send(new DebugCommand.Add(text));

                //    ColoredVertex[] vertices = new ColoredVertex[] {
                //        new ColoredVertex(new Vec2Float(7,7), new Vec2Float(), colorRed),
                //        new ColoredVertex(new Vec2Float(17,7), new Vec2Float(), colorRed),
                //        new ColoredVertex(new Vec2Float(20,20), new Vec2Float(), colorRed),
                //        new ColoredVertex(new Vec2Float(10,10), new Vec2Float(), colorRed)
                //    };
                //    DebugData.Primitives lines = new DebugData.Primitives(vertices, PrimitiveType.Lines);
                //    debugInterface.Send(new DebugCommand.Add(lines));
                //}
                #endregion
                //_debugInterface.Send(new DebugCommand.Flush());
            }
        }
        void GeneratePotencAttackMap()
        {
            potencAttackMap.Reset();

            // составляем список лучников
            List<int> rangersId = new List<int>();
            foreach (var e in entityMemories)
            {
                if (e.Value.myEntity.EntityType == EntityType.RangedUnit)
                {
                    rangersId.Add(e.Key);
                }
            }

            Dictionary<int, PotencAttackCell> enemyRangersDistByID = new Dictionary<int, PotencAttackCell>();
            Dictionary<int, PotencAttackCell> enemyTurretDistByID = new Dictionary<int, PotencAttackCell>();

            // составляем список врагов
            foreach (var p in enemiesById)
            {
                EntityType entityType = p.Value.EntityType;
                if (entityType == EntityType.RangedUnit)
                {
                    enemyRangersDistByID.Add(p.Key, new PotencAttackCell());
                }
                else if (entityType == EntityType.Turret)
                {
                    if (p.Value.Active)
                        enemyTurretDistByID.Add(p.Key, new PotencAttackCell());
                }
            }
            // считаем для каждого врага на каком к нему расстоянии находятся лучники
            foreach (var enemy in enemyRangersDistByID)
            {
                int x1 = enemiesById[enemy.Key].Position.X;
                int y1 = enemiesById[enemy.Key].Position.Y;
                foreach (var myRangerId in rangersId)
                {
                    int x2 = entityMemories[myRangerId].myEntity.Position.X;
                    int y2 = entityMemories[myRangerId].myEntity.Position.Y;
                    int dist = Abs(x1 - x2) + Abs(y1 - y2);
                    if (dist <= 5) enemy.Value.dist5low++;
                    else if (dist == 6) enemy.Value.dist6++;
                    else if (dist == 7) enemy.Value.dist7++;
                    else if (dist == 8) enemy.Value.dist8++;
                }
            }
            foreach (var enemy in enemyTurretDistByID)
            {
                int x1 = enemiesById[enemy.Key].Position.X;
                int y1 = enemiesById[enemy.Key].Position.Y;
                foreach (var myRangerId in rangersId)
                {
                    int x2 = entityMemories[myRangerId].myEntity.Position.X;
                    int y2 = entityMemories[myRangerId].myEntity.Position.Y;
                    int dist1 = Abs(x1 - x2) + Abs(y1 - y2);
                    int dist2 = Abs(x1 + 1 - x2) + Abs(y1 - y2);
                    int dist3 = Abs(x1 - x2) + Abs(y1 + 1 - y2);
                    int dist4 = Abs(x1 + 1 - x2) + Abs(y1 + 1 - y2);
                    int dist = Min(Min(Min(dist1, dist2),dist3),dist4);
                    if (dist <= 5) enemy.Value.dist5low++;
                    else if (dist == 6) enemy.Value.dist6++;
                    else if (dist == 7) enemy.Value.dist7++;
                    else if (dist == 8) enemy.Value.dist8++;
                }
            }

            /// теперь каждый противник разбрасывает маяки вокруг себя
            /// если вокруг турели на дистанции 5 есть лучники, то маяки на 5
            /// если на дист 6 больше 4 лучников, то маяки на 5, иначи маяяки на 6
            foreach (var enemy in enemyTurretDistByID)
            {
                int attackRange = 6;
                if (enemy.Value.dist5low > 0) attackRange = 5;
                else if (enemy.Value.dist6 >= 4) attackRange = 5;

                int sx = enemiesById[enemy.Key].Position.X;
                int sy = enemiesById[enemy.Key].Position.Y;
                int size = properties[EntityType.Turret].Size;
                int sxRight = sx + size - 1;
                int syUp = sy + size - 1;
                for (int k = attackRange; k >= 1; k--)
                {
                    for (int cc = 0; cc < k; cc++)
                    {
                        potencAttackMap.AddCell(sx - k + cc, sy - cc, k, k == attackRange);//left-down
                        potencAttackMap.AddCell(sx - cc, syUp + k - cc, k, k == attackRange);//left-up
                        potencAttackMap.AddCell(sxRight + k - cc, syUp + cc, k, k == attackRange);//right-up
                        potencAttackMap.AddCell(sxRight + cc, sy - k + cc, k, k == attackRange);//right-down
                    }
                    potencAttackMap.AddCell(sx - k, syUp, k, k == attackRange);//left
                    potencAttackMap.AddCell(sxRight, syUp + k, k, k == attackRange);//up
                    potencAttackMap.AddCell(sxRight + k, sy, k, k == attackRange);//right
                    potencAttackMap.AddCell(sx, sy - k, k, k == attackRange);//down 
                }
            }
            /// если вокруг лучника есть мои на 5, то маяк на 5
            /// если есть 2+ на 6, то маяк на 5
            /// если есть 2+ на 7, то маяк на 6
            /// иначе маяк на 7
            foreach (var enemy in enemyRangersDistByID)
            {
                int attackRange = 7;
                if (enemy.Value.dist5low > 0) attackRange = 5;
                else if (enemy.Value.dist6 > 1) attackRange = 5;
                else if (enemy.Value.dist7 > 1) attackRange = 6;

                int sx = enemiesById[enemy.Key].Position.X;
                int sy = enemiesById[enemy.Key].Position.Y;
                int size = properties[EntityType.RangedUnit].Size;
                int sxRight = sx + size - 1;
                int syUp = sy + size - 1;
                for (int k = attackRange; k >= 1; k--)
                {
                    for (int cc = 0; cc < k; cc++)
                    {
                        potencAttackMap.AddCell(sx - k + cc, sy - cc, k, k == attackRange);//left-down
                        potencAttackMap.AddCell(sx - cc, syUp + k - cc, k, k == attackRange);//left-up
                        potencAttackMap.AddCell(sxRight + k - cc, syUp + cc, k, k == attackRange);//right-up
                        potencAttackMap.AddCell(sxRight + cc, sy - k + cc, k, k == attackRange);//right-down
                    }
                }

            }
            potencAttackMap.CalcMin();
        }
        void GeneratePotencTargetMap()
        {
            potencTarget5Map.Reset();

            foreach(var en in _playerView.Entities)
            {
                if (en.PlayerId != myId) // enemies or resources
                {
                    potencTarget5Map.Emit(
                        en.Position.X, 
                        en.Position.Y, 
                        en.EntityType, 
                        AttackDistanceByType.Get(en.EntityType)
                        );                    
                }
            }

            potencTarget5Map.CheckAll();
        }
        void DrawPotencTarget5Map()
        {
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    int textSize = 16;
                    if (potencTarget5Map[x, y].CanAttackWarriors == true)
                        DrawCenterCellText(x, y, colorRed, "w", textSize);
                    else if (potencTarget5Map[x, y].CanAttackTurret == true)
                        DrawCenterCellText(x, y, colorRed, "t", textSize);
                    else if (potencTarget5Map[x, y].CanAttackBuilder == true)
                        DrawCenterCellText(x, y, colorBlue, "b", textSize);
                    else if (potencTarget5Map[x, y].CanAttackBase == true)
                        DrawCenterCellText(x, y, colorGreen, "S", textSize);
                    else if (potencTarget5Map[x, y].CanAttackHouse == true)
                        DrawCenterCellText(x, y, colorGreen, "H", textSize);
                    else if (potencTarget5Map[x, y].CanAttackResource == true)
                        DrawCenterCellText(x, y, colorWhite, "R", textSize);
                    else if (potencTarget5Map[x, y].CanAttackWall == true)
                        DrawCenterCellText(x, y, colorBlack, "H", textSize);


                    //if (potencTarget5Map[x, y][EntityType.BuilderBase] > 0)
                    //    DrawCenterCellText(x, y, colorRed, potencTarget5Map[x, y][EntityType.BuilderBase], textSize);
                    //if (potencTarget5Map[x, y][EntityType.RangedBase] > 0)
                    //    DrawCenterCellText(x, y, colorRed, potencTarget5Map[x, y][EntityType.RangedBase], textSize);
                    //if (potencTarget5Map[x, y][EntityType.MeleeBase] > 0)
                    //    DrawCenterCellText(x, y, colorRed, potencTarget5Map[x, y][EntityType.MeleeBase], textSize);


                }
            }
        }
        void DrawCenterCellText(int x, int y, Color color, string text, int textSize)
        {
            ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y + 0.2f), new Vec2Float(0, 0), color);            
            _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, text, 0.5f, textSize)));
        }
        void DrawCenterCellText(int x, int y, Color color, int textInt, int textSize)
        {
            DrawCenterCellText(x, y, color, textInt.ToString(), textSize);
        }
        void DrawCenterCellTextSafe(int x, int y, Color color, string text, int textSize, DebugOptions option)
        {
            if (debugOptions[(int)DebugOptions.canDrawGetAction] == true && debugOptions[(int)option] == true)
                DrawCenterCellText(x, y, color, text, textSize);            
        }

        void SelectRangedBasePotencPlace()
        {
            bool place1find = false; // место где уже можно ставить базу
            bool place2find = false; // место ближе к стартовой точке, где надо убрать минимальное количество ресурсов
            if (basicEntityIdGroups[EntityType.RangedBase].members.Count == 0)
            {
                int buildingSize = properties[EntityType.RangedBase].Size;
                int place2radius = 7;
                int minCountResources = 25;

                int sx = 15;
                int sy = 15;
                int maxFind = 70;
                int flag = 3;   //  /2  \3
                int dx = 0;     //  \1  /0
                int dy = 0; // with my cell
                            //int flag = 0;   //  /2  \3
                            //int dx = 1;     //  \1  /0
                            //int dy = 0; // without my cell
                for (int step = 0; step <= maxFind;)
                {
                    // отмечаем
                    int nx = sx + dx;
                    int ny = sy + dy;
                    if (nx >= 0 && nx < mapSize && ny >= 0 && ny < mapSize)
                    {
                        if (place1find == false)
                        {
                            if (buildBarrierMap[nx, ny].CanBuildAfter(buildingSize))
                            {
                                place1find = true;
                                rangedBasePotencPlace1 = new Vec2Int(nx, ny);
                            }
                        }
                        if (step < place2radius)
                        {
                            if (onceVisibleMap[nx][ny] > 0)
                            {
                                if (buildBarrierMap[nx, ny].s5noBaseOrWarriorBarrier == true
                                    && buildBarrierMap[nx, ny].s5noEnemiesBarrier == true
                                    && buildBarrierMap[nx, ny].s5noUnvisivleBarrier == true)
                                {
                                    if (buildBarrierMap[nx, ny].s5howManyResBarrier < minCountResources)
                                    {
                                        minCountResources = buildBarrierMap[nx, ny].s5howManyResBarrier;
                                        place2find = true;
                                        rangedBasePotencPlace2 = new Vec2Int(nx, ny);
                                    }
                                }
                            }
                        }
                        else if (place1find == true)
                        {
                            break;
                        }

                    }

                    //двигаем цель
                    if (flag == 0)
                    {
                        dx--;
                        dy--;
                        if (dx == 0) flag = 1;
                    }
                    else if (flag == 1)
                    {
                        dx--;
                        dy++;
                        if (dy == 0) flag = 2;
                    }
                    else if (flag == 2)
                    {
                        dx++;
                        dy++;
                        if (dx == 0) flag = 3;
                    }
                    else if (flag == 3)
                    {
                        dx++;
                        dy--;
                        if (dy == 0)
                        {
                            flag = 0;
                            dx++;
                            step++;
                        }
                        else if (dy < 0)// first shift from 0,0
                        {
                            dx = 1;
                            dy = 0;
                            flag = 0;
                            step++;
                        }

                    }
                }

            }
            if (place1find == false)
                rangedBasePotencPlace1 = new Vec2Int(-10, -10);
            if (place2find == false)
                rangedBasePotencPlace2 = new Vec2Int(-10, -10);
        }
        void DrawPotencMapOverMyUnits(int dist)
        {
            foreach (var p in entityMemories)
            {
                EntityType entityType = p.Value.myEntity.EntityType;
                if (properties[entityType].CanMove == true)
                {
                    int sx = p.Value.myEntity.Position.X;
                    int sy = p.Value.myEntity.Position.Y;
                    int flag = 3;   //  /2  \3
                    int dx = 0;     //  \1  /0
                    int dy = 0; // with my cell
                    //int flag = 0;   //  /2  \3
                    //int dx = 1;     //  \1  /0
                    //int dy = 0; // without my cell
                    for (int step = 0; step <= dist;)
                    {
                        //рисуем
                        int nx = sx + dx;
                        int ny = sy + dy;
                        potencAttackMap.TryDraw(nx, ny, _debugInterface);                        

                        //двигаем цель
                        if (flag == 0)
                        {
                            dx--;
                            dy--;
                            if (dx == 0) flag = 1;
                        }
                        else if (flag == 1)
                        {
                            dx--;
                            dy++;
                            if (dy == 0) flag = 2;
                        }
                        else if (flag == 2)
                        {
                            dx++;
                            dy++;
                            if (dx == 0) flag = 3;
                        }
                        else if (flag == 3)
                        {
                            dx++;
                            dy--;
                            if (dy == 0)
                            {
                                flag = 0;
                                dx++;
                                step++;
                            }
                            else if (dy < 0)// first shift from 0,0
                            {
                                dx = 1;
                                dy = 0;
                                flag = 0;
                                step++;
                            }

                        }
                    }
                }
            }
        }
        void DrawPotencMapAll()
        {
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    potencAttackMap.TryDraw(x, y, _debugInterface);
                }
            }
        }
        void UpdateInteresMap()
        {
            for (int ix = 0; ix < interesMap.cellCount; ix++)
            {
                for (int iy = 0; iy < interesMap.cellCount; iy++)
                {
                    InteresQuad quad = interesMap[ix, iy];

                    bool isMyTerritory = false;
                    int myBuilders = 0;
                    bool isEnemyBase = false;
                    // bool isMyBorder = false;
                    int visibleCells = 0;
                    int enemyMelees = 0;
                    int enemyRangers = 0;
                    int enemyBuilders = 0;

                    for (int x = quad.X1; x <= quad.X2; x++)
                    {
                        for (int y = quad.Y1; y <= quad.Y2; y++)
                        {
                            if (currentVisibleMap[x][y] == true)
                                visibleCells++;
                            int id = cellWithIdAny[x][y];
                            if (id > 0)
                            {
                                if (entityMemories.ContainsKey(id))
                                {
                                    EntityType type = entityMemories[id].myEntity.EntityType;
                                    if (properties[type].CanMove)
                                    {
                                        if (type == EntityType.BuilderUnit)
                                        {
                                            myBuilders++;
                                        }
                                    } else
                                    {
                                        isMyTerritory = true;
                                    }

                                } else if (enemiesById.ContainsKey(id))
                                {
                                    EntityType type = enemiesById[id].EntityType;
                                    if (properties[type].CanMove)
                                    {
                                        if (type == EntityType.BuilderUnit)
                                        {
                                            //isEnemyBase = true;
                                            enemyBuilders++;
                                        } else
                                        {
                                            if (type == EntityType.RangedUnit)
                                                enemyRangers++;
                                            else
                                                enemyMelees++;
                                        }
                                    }
                                    else
                                    {
                                        isEnemyBase = true;
                                    }
                                }
                            }
                        }
                    }
                    if (myBuilders >= 3)
                        isMyTerritory = true;

                    quad.NextTick(_playerView.CurrentTick, isMyTerritory, isEnemyBase, enemyMelees, enemyRangers, enemyBuilders, visibleCells);
                }
            }
            for (int ix = 0; ix < interesMap.cellCount; ix++)
            {
                for (int iy = 0; iy < interesMap.cellCount; iy++)
                {
                    InteresQuad quad = interesMap[ix, iy];

                    if (quad._isMyTerritory)
                    {
                        interesMap.CheckBorder(ix, iy);
                    }
                }
            }

        }
        void DrawInteresMap()
        {
            for (int ix = 0; ix < interesMap.cellCount; ix++)
            {
                for (int iy = 0; iy < interesMap.cellCount; iy++)
                {
                    InteresQuad quad = interesMap[ix, iy];
                    float tx = quad.X1;
                    float ty = quad.Y2;
                    float step = 1f;
                    int textSize = 16;
                    if (quad._isExplored)
                    {
                        ColoredVertex position = new ColoredVertex(new Vec2Float(tx, ty), new Vec2Float(0, 0), colorWhite);
                        _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, "Ex", 0f, textSize)));
                    }
                    tx += step;
                    if (quad._isMyTerritory)
                    {
                        ColoredVertex position = new ColoredVertex(new Vec2Float(tx, ty), new Vec2Float(0, 0), colorGreen);
                        _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, "My", 0f, textSize)));
                    }
                    tx += step;
                    if (quad._isMyBorder)
                    {
                        ColoredVertex position = new ColoredVertex(new Vec2Float(tx, ty), new Vec2Float(0, 0), colorBlue);
                        _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, "Bo", 0f, textSize)));
                    }
                    ty -= step;
                    tx = quad.X1;
                    if (quad._isEnemyWarriors)
                    {
                        ColoredVertex position = new ColoredVertex(new Vec2Float(tx, ty), new Vec2Float(0, 0), colorRed);
                        _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, "UA", 0f, textSize)));
                    }
                    tx += step;
                    if (quad._isEnemyBase)
                    {
                        ColoredVertex position = new ColoredVertex(new Vec2Float(tx, ty), new Vec2Float(0, 0), colorMagenta);
                        _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, "Ba", 0f, textSize)));
                    }
                    tx += step;
                    if (quad._isEnemyBuilders)
                    {
                        ColoredVertex position = new ColoredVertex(new Vec2Float(tx, ty), new Vec2Float(0, 0), colorGreen);
                        _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, "Bu", 0f, textSize)));
                    }
                    DrawLineOnce(quad.X1, quad.Y2 + 1, quad.X2 + 1, quad.Y2 + 1, colorBlack, colorBlack);
                    DrawLineOnce(quad.X2 + 1, quad.Y1, quad.X2 + 1, quad.Y2 + 1, colorBlack, colorBlack);

                }
            }
        }
        void DrawMemoryEnemies()
        {
            foreach(var en in enemiesById)
            {
                if (properties[en.Value.EntityType].CanMove)
                {
                    if (currentVisibleMap[en.Value.Position.X][en.Value.Position.Y] == false)
                    {
                        DrawQuad(en.Value.Position.X + 0.1f, en.Value.Position.Y + 0.1f, en.Value.Position.X + 0.9f, en.Value.Position.Y + 0.9f, colorRed);
                    }
                }
            }
        }
        void DrawMemoryResources()
        {
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    if (currentVisibleMap[x][y] == false)
                    {
                        if (resourceMemoryMap[x][y] > 0)
                        {
                            DrawQuad(x + 0.1f, y + 0.1f, x + 0.9f, y + 0.9f, colorGreen);
                        }
                    }
                }
            }
        }

        #region draw dead statistics
        float drawDeadStatisticDX = -120;
        float drawDeadStatisticDY = -17;
        float drawDeadStatisticStepX = -50;
        float drawDeadStatisticStepY = -21;
        void DrawDeadStatistic()
        {
            int myPlayerNum = 0;
            for (int i = 0; i <_playerView.Players.Length; i++)
            {
                if (_playerView.Players[i].Id == myId)
                {
                    myPlayerNum = i;
                }
            }
            //myPlayerNum = _playerView.CurrentTick % 4;
            Color[] playerColors = new Color[] { 
                new Color(0.5f, 0.5f, 1, 1),
                new Color(0.5f, 0.9f, 0.5f, 1),
                new Color(1, 0.5f, 0.5f, 1),
                new Color(1, 0.8f, 0.5f, 1) 
            };

            float x = _debugInterface.GetState().WindowSize.X / 2 + drawDeadStatisticDX;
            float y = _debugInterface.GetState().WindowSize.Y + drawDeadStatisticDY + drawDeadStatisticStepY * myPlayerNum;
            int textSize = 16;

            string text;
            text = $"{currentMyEntityCount[EntityType.MeleeUnit]}/{myDeadMelees}m";
            _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(
                new ColoredVertex(null, new Vec2Float(x, y), playerColors[myPlayerNum]), text, 1f, textSize)));
            x += drawDeadStatisticStepX;
            text = $"{currentMyEntityCount[EntityType.BuilderUnit]}/{myDeadBuilders}b";            
            _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(
                new ColoredVertex(null, new Vec2Float(x, y), playerColors[myPlayerNum]), text, 1f, textSize)));
            x += drawDeadStatisticStepX;
            text = $"{currentMyEntityCount[EntityType.RangedUnit]}/{myDeadRangers}r";
            _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(
                new ColoredVertex(null, new Vec2Float(x, y), playerColors[myPlayerNum]), text, 1f, textSize)));
            x += drawDeadStatisticStepX;
        }
        #endregion

        void GenerateDesires()
        {
            prevDesires.Clear();
            prevDesires = desires;
            desires = new List<DesireType>();

            #region Хочу строить базу лучников


            bool needMakeRangedBase = false;
            if (basicEntityIdGroups[EntityType.RangedBase].members.Count == 0)
            {

                int maxEnemyResources = 0;
                foreach (var pl in _playerView.Players)
                {
                    if (pl.Id != myId)
                    {
                        if (pl.Resource > maxEnemyResources)
                        {
                            maxEnemyResources = pl.Resource;
                        }
                    }
                }
                if (maxEnemyResources > 300)
                {
                    opponentHasResourcesForRangersBase = true;
                }

                if (_playerView.CurrentTick > 200 || opponentHasResourcesForRangersBase && _playerView.CurrentTick > 150)
                {
                    for (int x = 0; x < mapSize; x++)
                    {
                        for (int y = 0; y < mapSize; y++)
                        {
                            if (buildBarrierMap[x, y].CanBuildAfter(5))
                            {
                                desires.Add(DesireType.WantCreateRangerBase);
                                needMakeRangedBase = true;
                                break;
                            }
                        }
                        if (needMakeRangedBase == true)
                            break;
                    }

                }
            }
            else
            {
                if (entityMemories[basicEntityIdGroups[EntityType.RangedBase].members[0]].myEntity.Active == false)
                {
                    //needMakeRangedBase = true;
                }
            }

            #endregion


            #region Хочу строить дома
            if (needMakeRangedBase == false)
            {
                int[] popMax = new int[] { 15, 30, 55, 70, 100, 1000 };
                int[] popRange = new int[] { 0, 4, 8, 10, 10, 10 };
                for (int i = 0; i < popMax.Length; i++)
                {
                    int potencPopulation = populationMax;
                    foreach (var id in basicEntityIdGroups[EntityType.House].members)
                    {
                        if (entityMemories[id].myEntity.Active == false)
                            potencPopulation += properties[EntityType.House].PopulationProvide;
                    }
                    if (potencPopulation <= popMax[i])
                    {
                        if (populationUsing + popRange[i] >= potencPopulation)
                        {
                            desires.Add(DesireType.WantCreateHouses);
                            break;
                        }
                    }
                }
            }
            #endregion

            #region Выбираем какого юнита строить
            //if (needMakeRangedBase == false)
            {
                int countEnemiesOnMyTerritory = 0;
                int myTerritoryX = mapSize / 2;
                int myTerritoryY = mapSize / 2;
                foreach (var p in enemiesById)
                {
                    if (p.Value.Position.X < myTerritoryX && p.Value.Position.Y < myTerritoryY)
                    {
                        countEnemiesOnMyTerritory++;
                    }
                }

                bool needCreateWarriors = false;
                if (iHaveActiveRangedBase == true && iHaveActiveBuilderBase == true)
                {
                    if (countEnemiesOnMyTerritory > 0)
                    {
                        /// тесты показали эффективность этого расчета над
                        /// + populationMax / 3
                        /// + populationMax / 4
                        /// + populationMax / 5
                        /// соответствует версии basic_retreat1 до исправления ошибки подсчета populationMax
                        /// см. 10.12_13:51 и 10.12_14:00 и 10.12_14:19
                        int potencyPopul = 0;
                        foreach (var e in properties)
                        {
                            potencyPopul += currentMyEntityCount[e.Key] * e.Value.PopulationProvide;
                        }

                        if (currentMyEntityCount[EntityType.MeleeUnit] + currentMyEntityCount[EntityType.RangedUnit] <= countEnemiesOnMyTerritory + potencyPopul / 5)
                        {
                            needCreateWarriors = true;
                            desires.Add(DesireType.WantCreateRangers);
                        }
                    }
                    if (needCreateWarriors == false)
                    {
                        if (currentMyEntityCount[EntityType.BuilderUnit] < 30)
                        {
                            desires.Add(DesireType.WantCreateBuilders);
                        }
                        else if (currentMyEntityCount[EntityType.BuilderUnit] > 70)
                        {
                            desires.Add(DesireType.WantCreateRangers);
                        }
                        else
                        {
                            if (currentMyEntityCount[EntityType.BuilderUnit] * ratioRangedToBuilder < currentMyEntityCount[EntityType.RangedUnit])
                                desires.Add(DesireType.WantCreateBuilders);
                            else
                                desires.Add(DesireType.WantCreateRangers);
                        }
                    }
                } else if (iHaveActiveRangedBase == true && iHaveActiveBuilderBase == false) // нет базы строителей
                {
                    if (populationUsing < populationMax)
                        desires.Add(DesireType.WantCreateRangers);
                }
                else if (iHaveActiveRangedBase == false && iHaveActiveBuilderBase == true)// нет базы лучников
                {
                    if (populationUsing < populationMax)
                        desires.Add(DesireType.WantCreateBuilders);
                }
            }
            #endregion

            #region хочу строить турели

            if (myResources > buildTurretThenResourcesOver)
            {
                if (basicEntityIdGroups[EntityType.House].members.Count > basicEntityIdGroups[EntityType.Turret].members.Count)
                    desires.Add(DesireType.WantCreateTurret);
            }

            #endregion

            desires.Add(DesireType.WantRetreatBuilders);
            desires.Add(DesireType.WantCollectResources);

            desires.Add(DesireType.WantTurretAttacks);
            desires.Add(DesireType.WantAllMeleesAttack);

            if (needRepairBuildingIdList.Count > 0)
                desires.Add(DesireType.WantRepairBuildings);


            //// retreat from enemies
            /// info units about dangers zone

            ////build units
            /// build builder
            /// build ranger
            /// build melee

            //// build bases
            /// build BuilderBase
            /// build RangedBase
            /// build MeleeBase
            /// build house
            /// build turret
            /// build wall

            //// attack enemies
            /// my builder charge enemy builders

            //// collect resources
        }
        void ConvertDesiresToPlans()
        {
            prevPlans.Clear();
            prevPlans = plans;
            plans = new List<PlanType>();

            //add new plans
            foreach (var d in desires)
            {
                switch (d)
                {
                    case DesireType.WantCreateBuilders:
                        #region хочу создавать строителей
                        //i have base
                        if (currentMyEntityCount[EntityType.BuilderBase] > 0)
                        {
                            //i have resources
                            int newCost = properties[EntityType.BuilderUnit].InitialCost + currentMyEntityCount[EntityType.BuilderUnit] - 1;
                            if (howMuchResourcesIHaveNextTurn >= newCost)
                            {
                                myResources -= newCost;
                                plans.Add(PlanType.PlanCreateBuilders);
                            }
                        }
                        break;
                    #endregion
                    case DesireType.WantCreateRangers:
                        #region Хочу создавать стрелков
                        //i have base
                        if (currentMyEntityCount[EntityType.RangedBase] > 0)
                        {
                            //i have resources
                            int newCost = properties[EntityType.RangedUnit].InitialCost + currentMyEntityCount[EntityType.RangedUnit] - 1;
                            if (howMuchResourcesIHaveNextTurn >= newCost)
                            {
                                myResources -= newCost;
                                plans.Add(PlanType.PlanCreateRangers);
                            }
                        }
                        break;
                    #endregion
                    case DesireType.WantCreateHouses:
                        #region хочу строить дома
                        //i have builders
                        if (currentMyEntityCount[EntityType.BuilderUnit] > 0)
                        {
                            //i have resources
                            int newCost = properties[EntityType.House].InitialCost;
                            if (myResources >= newCost)
                            {
                                // ограничение на одновременное строительство
                                int count = 0;
                                foreach (var en in entityMemories)
                                {
                                    if (en.Value.myEntity.Active == false)
                                        count++;
                                }
                                if ((populationMax <= 20 && count == 0)
                                    || (populationMax > 20 && populationMax <= 40 && count <= 1)
                                    || (populationMax > 40 && populationMax <= 60 && count <= 2)
                                    || (populationMax > 60 && count <= 2))
                                {
                                    myResources -= newCost;
                                    plans.Add(PlanType.PlanCreateHouses);
                                }
                            }
                        }
                        break;
                    #endregion
                    case DesireType.WantCreateRangerBase:
                        #region хочу строить базу
                        //i have builders
                        if (currentMyEntityCount[EntityType.BuilderUnit] > 0)
                        {
                            //i have resources
                            int newCost = properties[EntityType.RangedBase].InitialCost;
                            if (myResources >= newCost)
                            {
                                myResources -= newCost;
                                plans.Add(PlanType.PlanCreateRangerBase);
                            }
                        }
                        break;
                    #endregion
                    case DesireType.WantCreateTurret:
                        #region хочу строить базу
                        //i have builders
                        if (currentMyEntityCount[EntityType.BuilderUnit] > 0)
                        {
                            //i have resources
                            int newCost = properties[EntityType.Turret].InitialCost;
                            if (myResources >= newCost)
                            {
                                plans.Add(PlanType.PlanCreateTurret);
                            }
                        }
                        break;
                    #endregion
                    case DesireType.WantRepairBuildings:
                        #region хочу ремонтировать здания
                        {
                            bool needRepairOld = false;
                            bool needRepairNew = false;
                            foreach (var id in needRepairBuildingIdList)
                            {
                                if (entityMemories[id].myEntity.Active == true)
                                    needRepairOld = true;
                                else
                                    needRepairNew = true;
                            }
                            if (needRepairNew)
                                plans.Add(PlanType.PlanRepairNewBuildings);
                            if (needRepairOld)
                                plans.Add(PlanType.PlanRepairOldBuildings);
                        }
                        break;
                    #endregion
                    case DesireType.WantRetreatBuilders:
                        #region хочу чтобы строители сбегали от врагов
                        plans.Add(PlanType.PlanRetreatBuilders);
                        break;
                    #endregion
                    case DesireType.WantCollectResources:
                        #region хочу добывать ресурсы
                        //i have builders
                        if (currentMyEntityCount[EntityType.BuilderUnit] > 0)
                        {
                            plans.Add(PlanType.PlanExtractResources);
                        }
                        break;
                    #endregion
                    case DesireType.WantTurretAttacks:
                        #region хочу чтобы турели атаковали
                        //i have turret
                        if (currentMyEntityCount[EntityType.Turret] > 0)
                        {
                            plans.Add(PlanType.PlanTurretAttacks);
                        }
                        break;
                    #endregion
                    case DesireType.WantAllMeleesAttack:
                        #region хочу чтобы все войны атаковали
                        //i have warrior
                        if (currentMyEntityCount[EntityType.MeleeUnit] > 0)
                        {
                            plans.Add(PlanType.PlanAllMeleesAttack);
                        }
                        break;
                    #endregion
                    default:
                        throw new System.Exception("Неизвестный тип Желаний");//unknown type                        
                }
            }
        }
        void PrepareBeforeCreateIntentions()
        {
            #region очищаем группы побега и атаки строителей
            while (groupRetreatBuilders.members.Count > 0)
            {
                entityMemories[groupRetreatBuilders.members[0]].SetGroup(basicEntityIdGroups[entityMemories[groupRetreatBuilders.members[0]].myEntity.EntityType]);
            }
            while (groupMyBuildersAttackEnemyBuilders.members.Count > 0)
            {
                entityMemories[groupMyBuildersAttackEnemyBuilders.members[0]].SetGroup(basicEntityIdGroups[entityMemories[groupMyBuildersAttackEnemyBuilders.members[0]].myEntity.EntityType]);
            }
            #endregion

        }
        void ConvertPlansToIntentions()
        {
            prevIntentions.Clear();
            prevIntentions = intentions;
            intentions = new List<Intention>();
            foreach (var d in plans)
            {
                switch (d)
                {
                    case PlanType.PlanCreateBuilders:
                        foreach (var id in basicEntityIdGroups[EntityType.BuilderBase].members)
                        {
                            intentions.Add(new Intention(IntentionType.IntentionCreateBuilder, id));
                        }
                        break;
                    case PlanType.PlanCreateRangers:
                        foreach (var id in basicEntityIdGroups[EntityType.RangedBase].members)
                        {
                            intentions.Add(new Intention(IntentionType.IntentionCreateRanger, id));
                        }
                        break;
                    case PlanType.PlanCreateHouses:
                        // выбираем где будем строить
                        {
                            Vec2Int pos = FindPositionFromOurCorner(EntityType.House);
                            if (pos.X >= 0)
                            {
                                Intention intention = new Intention(IntentionType.IntentionCreateHouse, pos, EntityType.House);
                                intentions.Add(intention);
                            }
                        }
                        break;
                    case PlanType.PlanCreateRangerBase:
                        {
                            Vec2Int pos = FindPositionForRangedBase();
                            if (pos.X >= 0)
                            {
                                Intention intention = new Intention(IntentionType.IntentionCreateRangedBase, pos, EntityType.RangedBase);
                                intentions.Add(intention);
                            }
                        }
                        break;
                    case PlanType.PlanCreateTurret:
                        {
                            Vec2Int pos = FindPositionFromOurCorner(EntityType.Turret);
                            if (pos.X >= 0)
                            {
                                Intention intention = new Intention(IntentionType.IntentionCreateTurret, pos, EntityType.Turret);
                                intentions.Add(intention);
                            }
                        }
                        break;
                    case PlanType.PlanRepairNewBuildings:
                        foreach (var id in needRepairBuildingIdList)
                        {
                            if (entityMemories[id].myEntity.Active == false)
                                intentions.Add(new Intention(IntentionType.IntentionRepairNewBuilding, id));
                        }
                        break;
                    case PlanType.PlanRepairOldBuildings:
                        foreach (var id in needRepairBuildingIdList)
                        {
                            if (entityMemories[id].myEntity.Active == true)
                                intentions.Add(new Intention(IntentionType.IntentionRepairOldBuilding, id));
                        }
                        break;
                    case PlanType.PlanRetreatBuilders:
                        #region хочу чтобы строители сбегали от врагов
                        {
                            bool needRetreat = false;
                            bool needAttackEnemyBuilders = false;
                            foreach (var em in entityMemories)
                            {
                                if (em.Value.myEntity.EntityType == EntityType.BuilderUnit)
                                //|| em.Value.myEntity.EntityType == EntityType.MeleeUnit
                                //|| em.Value.myEntity.EntityType == EntityType.RangedUnit)
                                {
                                    EnemyDangerCell enemyDangerCell = enemyDangerCells[em.Value.myEntity.Position.X][em.Value.myEntity.Position.Y];
                                    if ((enemyDangerCell.meleesWarning + enemyDangerCell.meleesAim + enemyDangerCell.rangersAim + enemyDangerCell.rangersWarning) > 0)
                                    {
                                        needRetreat = true;
                                        em.Value.SetGroup(groupRetreatBuilders);
                                    }
                                    else
                                    {
                                        if (enemyDangerCell.buildersAim > 0)
                                        {
                                            needAttackEnemyBuilders = true;
                                            em.Value.SetGroup(groupMyBuildersAttackEnemyBuilders);
                                        }
                                    }
                                }
                            }
                            if (needRetreat)
                                intentions.Add(new Intention(IntentionType.IntentionRetreatBuilders, groupRetreatBuilders));
                            if (needAttackEnemyBuilders)
                                intentions.Add(new Intention(IntentionType.IntentionMyBuiAttackEnemyBui, groupMyBuildersAttackEnemyBuilders));
                        }
                        break;
                    #endregion
                    case PlanType.PlanExtractResources:
                        intentions.Add(new Intention(IntentionType.IntentionExtractResources, basicEntityIdGroups[EntityType.BuilderUnit]));
                        break;
                    case PlanType.PlanTurretAttacks:
                        intentions.Add(new Intention(IntentionType.IntentionTurretAttacks, basicEntityIdGroups[EntityType.Turret]));
                        break;
                    case PlanType.PlanAllMeleesAttack:
                        intentions.Add(new Intention(IntentionType.IntentionAllWarriorsAttack, basicEntityIdGroups[EntityType.MeleeUnit]));                        
                        break;
                    default:
                        throw new System.Exception("неучтенный план");
                }
            }
        }
        void CorrectCrossIntentions()
        {
            for (int i = 0; i < prevIntentions.Count;)
            {
                bool delete = false;
                // анализ предыдущих заданий
                switch (prevIntentions[i].intentionType)
                {
                    case IntentionType.IntentionCreateBuilder:
                        #region  cancel base build 
                        {
                            if (entityMemories.ContainsKey(prevIntentions[i].targetId)) // на случай если в этом ходу сущность уже мертва
                            {
                                bool needStop = true;
                                foreach (var ni in intentions)
                                {
                                    if (ni.intentionType == IntentionType.IntentionCreateBuilder)
                                    {
                                        if (prevIntentions[i].targetId == ni.targetId)
                                        {
                                            needStop = false;
                                            break;
                                        }
                                    }
                                }
                                if (needStop)
                                    intentions.Add(new Intention(IntentionType.IntentionStopCreatingBuilder, prevIntentions[i].targetId));
                            }
                        }
                        break;
                    #endregion
                    case IntentionType.IntentionCreateRanger:
                        #region // cancel Ranger base build
                        {
                            if (entityMemories.ContainsKey(prevIntentions[i].targetId)) // на случай если в этом ходу сущность уже мертва
                            {
                                bool needStop = true;
                                foreach (var ni in intentions)
                                {
                                    if (ni.intentionType == IntentionType.IntentionCreateRanger)
                                    {
                                        if (prevIntentions[i].targetId == ni.targetId)
                                        {
                                            needStop = false;
                                            break;
                                        }
                                    }
                                }
                                if (needStop)
                                {
                                    intentions.Add(new Intention(IntentionType.IntentionStopCreatingRanger, prevIntentions[i].targetId));
                                }
                            }
                        }
                        break;
                    #endregion
                    case IntentionType.IntentionRepairNewBuilding:
                        #region продолжаем ремонтировать или отменяем задание
                        //{
                        //    bool removed = false;
                        //    int targetId = prevIntentions[i].targetId;

                        //    if (entityMemories.ContainsKey(prevIntentions[i].targetId))
                        //    {
                        //        if (entityMemories[targetId].myEntity.Health == properties[entityMemories[targetId].myEntity.EntityType].MaxHealth)
                        //        {
                        //            removed = true;
                        //        }
                        //    }
                        //    else
                        //    {
                        //        //target die
                        //        removed = true;
                        //    }

                        //    if (removed)
                        //    {
                        //        while (prevIntentions[i].targetGroup.members.Count > 0)
                        //        {
                        //            int id = prevIntentions[i].targetGroup.members[0];
                        //            entityMemories[id].SetGroup(basicEntityIdGroups[entityMemories[id].myEntity.EntityType]);
                        //        }
                        //    }
                        //    else
                        //    {
                        //        intentions.Add(prevIntentions[i]);
                        //    }
                        //}
                        break;
                        #endregion
                }

                if (delete)
                {
                    prevIntentions.RemoveAt(i);
                }
                else { i++; }
            }
        }

        void ConvertIntentionsToOrders()
        {
            foreach (var em in entityMemories)
            {
                em.Value.ResetTarget();
            }

            foreach (var ni in intentions)
            {
                switch (ni.intentionType)
                {
                    case IntentionType.IntentionCreateBuilder:
                        OrderCreateUnit(ni.targetId, false);
                        break;
                    case IntentionType.IntentionStopCreatingBuilder:
                        OrderCancelAll(ni.targetId);
                        break;
                    case IntentionType.IntentionCreateRanger:
                        OrderCreateUnit(ni.targetId, true);
                        break;
                    case IntentionType.IntentionStopCreatingRanger:
                        OrderCancelAll(ni.targetId);
                        break;
                    case IntentionType.IntentionExtractResources:
                        {
                            //int dist = properties[EntityType.BuilderUnit].SightRange;
                            foreach (var id in ni.targetGroup.members)
                            {
                                OrderCollectResources(id);
                            }
                        }
                        break;
                    case IntentionType.IntentionCreateTurret:
                    case IntentionType.IntentionCreateRangedBase:
                    case IntentionType.IntentionCreateHouse:
                        break;
                    case IntentionType.IntentionRepairOldBuilding:
                    case IntentionType.IntentionRepairNewBuilding:
                        break;
                    case IntentionType.IntentionTurretAttacks:
                        foreach (int id in ni.targetGroup.members)
                        {
                            OrderTurretAttack(id);
                        }
                        break;
                    case IntentionType.IntentionAllWarriorsAttack:
                        foreach (int id in ni.targetGroup.members)
                        {
                            OrderAttackNearbyEnemy(id, new EntityType[] { });
                        }
                        break;
                    case IntentionType.IntentionRetreatBuilders:
                        foreach (int id in ni.targetGroup.members)
                        {
                            entityMemories[id].OrderTryRetreat(false);
                            //OrderRetreatBuilderFromEnemy(id);
                        }
                        break;
                    case IntentionType.IntentionMyBuiAttackEnemyBui:
                        foreach (int id in ni.targetGroup.members)
                        {
                            OrderAttackNearbyEnemy(id, new EntityType[] { EntityType.BuilderUnit });
                        }
                        break;
                    default:
                        throw new System.Exception("Не учтенное намерение!");
                }
            }
        }
         
        void OptimizeOrders()
        {
            DrawOrderStatisticInit();
            DrawOrderStatistic("start");
            OptimizeOrderToDirectAttackRM(); // оптимизируем приказы на атаку воинов
            DrawOrderStatistic("DirAtt");
            OptimizeNearRangersMove(4); // двигаются те кто близок к врагу
            DrawOrderStatistic("NrRngMv");
            OptimizeWarriorsMove();
            DrawOrderStatistic("WrrMv");
            OptimizeSafeRangerAttack(); // атакуем безопасные цели: строителей, здания
            DrawOrderStatistic("SafeAtt");
            // OptimizeOrderToHealWarriors(); // оптимизируем приказы на лечение воинов
            OptimizeOrderToRetreat(); // оптимизируем отступление
            DrawOrderStatistic("Retreat");

            GenerateResourcePotentialField();

            OptimizeOrderToRepairNew(); // ремонтируем новые здания
            DrawOrderStatistic("RepNew");
            OptimizeOrderToBuildNew(); // строим новые здания
            DrawOrderStatistic("BldNew");
            OptimizeOrderToRepairOld(); // ремонтируем старые здания
            DrawOrderStatistic("RepOld");
            OptAttackBlindRangers(); // атакуем сквозь ресурсы теми рейнджерами, кто не получил приказа
            DrawOrderStatistic("MvAtRng");

        }

        class Target
        {
            public int _id;
            public EntityType _entityType;
            public int _health;
            public int _x;
            public int _y;
            public int _dist;
            public Target(int id, EntityType type, int health, int x, int y, int dist)
            {
                _id = id;
                _entityType = type;
                _health = health;
                _x = x;
                _y = y;
                _dist = dist;
            }
        }
        class EnemyToOpt
        {
            public Dictionary<int, Target> _targetsMyUnitsById;
            public Target _me;
            public EnemyToOpt(int id, EntityType type, int health, int x, int y)
            {
                _me = new Target(id, type, health, x, y, 0);
                _targetsMyUnitsById = new Dictionary<int, Target>();
            }
            public void Add(int id, EntityType type, int health, int x, int y, int dist)
            {
                _targetsMyUnitsById.Add(id, new Target(id, type, health, x, y, dist));
            }
            public void Add(Target targetMyUnit)
            {
                _targetsMyUnitsById.Add(targetMyUnit._id, targetMyUnit);
            }
            public int Count
            {
                get { return _targetsMyUnitsById.Count; }
            }
            public Target this[int i]
            {
                get { return _targetsMyUnitsById[i]; }
                set { _targetsMyUnitsById[i] = value; }
            }
        }
        void OptimizeOrderToDirectAttackRM()
        {
            int damageR = properties[EntityType.RangedUnit].Attack.Value.Damage;
            int damageM = properties[EntityType.MeleeUnit].Attack.Value.Damage;
            // рассматриваем тех кто уже воюет
            // распределяем цели
            Dictionary<int, List<EnemyToOpt>> myRangers = new Dictionary<int, List<EnemyToOpt>>();
            //List<int> enemyRangersId = new List<int>();
            //List<int> enemyMeleesId = new List<int>();
            Dictionary<int, EnemyToOpt> enemyRangers = new Dictionary<int, EnemyToOpt>();
            Dictionary<int, EnemyToOpt> enemyMelees = new Dictionary<int, EnemyToOpt>();

            foreach (var en in entityMemories)
            {
                if (en.Value.myEntity.EntityType == EntityType.RangedUnit)
                {
                    myRangers.Add(en.Key, new List<EnemyToOpt>());
                }
            }
            foreach (var en in enemiesById)
            {
                if (en.Value.EntityType == EntityType.RangedUnit)
                {
                    // enemyRangersId.Add(en.Key);
                    enemyRangers.Add(en.Key, new EnemyToOpt(en.Key, en.Value.EntityType, en.Value.Health, en.Value.Position.X, en.Value.Position.Y));
                }
                else if (en.Value.EntityType == EntityType.MeleeUnit)
                {
                    //enemyMeleesId.Add(en.Key);
                    enemyMelees.Add(en.Key, new EnemyToOpt(en.Key, en.Value.EntityType, en.Value.Health, en.Value.Position.X, en.Value.Position.Y));
                }
            }

            // собираем пары всех кто на дистанции до 5 включительно
            foreach (var my in myRangers)
            {
                int x1 = entityMemories[my.Key].myEntity.Position.X;
                int y1 = entityMemories[my.Key].myEntity.Position.Y;

                foreach (var en in enemyRangers)
                {
                    int x2 = enemiesById[en.Key].Position.X;
                    int y2 = enemiesById[en.Key].Position.Y;
                    int dist = Abs(x1 - x2) + Abs(y1 - y2);
                    if (dist <= 5)
                    {
                        my.Value.Add(en.Value);
                        en.Value._me._dist = dist;
                        en.Value.Add(new Target(my.Key, EntityType.RangedUnit, entityMemories[my.Key].myEntity.Health, x1, y1, dist));
                    }
                }

                foreach (var en in enemyMelees)
                {
                    int x2 = enemiesById[en.Key].Position.X;
                    int y2 = enemiesById[en.Key].Position.Y;
                    int dist = Abs(x1 - x2) + Abs(y1 - y2);
                    if (dist <= 5)
                    {
                        my.Value.Add(en.Value);
                        en.Value._me._dist = dist;
                        en.Value.Add(new Target(my.Key, EntityType.RangedUnit, entityMemories[my.Key].myEntity.Health, x1, y1, dist));
                    }
                }
            }

            #region чистим списки кто остался с пустыми парами
            List<int> deleteKeys = new List<int>();
            foreach (var i in myRangers)
            {
                if (i.Value.Count == 0)
                    deleteKeys.Add(i.Key);
            }
            foreach (var i in deleteKeys)
            {
                myRangers.Remove(i);
            }
            deleteKeys.Clear();
            foreach (var i in enemyMelees)
            {
                if (i.Value.Count == 0)
                    deleteKeys.Add(i.Key);
            }
            foreach (var i in deleteKeys)
            {
                enemyMelees.Remove(i);
            }
            deleteKeys.Clear();
            foreach (var i in enemyRangers)
            {
                if (i.Value.Count == 0)
                    deleteKeys.Add(i.Key);
            }
            foreach (var i in deleteKeys)
            {
                enemyRangers.Remove(i);
            }
            #endregion

            #region определяем действия тех, кто может стрелять только в одного c равной (стрелок в стрелка) или сильной позиции (стрелок в мечника с дистанции > 2)
            //bool wasRemoved;
            bool wasKilled;
            do
            {
                //wasRemoved = false;
                wasKilled = false;
                deleteKeys.Clear();
                foreach (var i in myRangers)
                {
                    if (i.Value.Count == 1)
                    {
                        if (i.Value[0]._me._entityType == EntityType.RangedUnit
                            || (i.Value[0]._me._entityType == EntityType.MeleeUnit && i.Value[0]._targetsMyUnitsById[i.Key]._dist > 2))
                        {
                            if (i.Value[0]._me._health > 0)
                            {
                                // remove my ranger, he do that he can
                                deleteKeys.Add(i.Key);
                                // create order
                                int x = entityMemories[i.Key].myEntity.Position.X;
                                int y = entityMemories[i.Key].myEntity.Position.Y;
                                nextPositionMyUnitsMap[x][y] = i.Key; // stop on position
                                int enemyId = i.Value[0]._me._id;
                                entityMemories[i.Key].OrderAttack(enemyId, null, true);
                                // draw attack line
                                if (debugOptions[(int)DebugOptions.drawOptAttack])
                                {
                                    DrawLineOnce(
                                        x + 0.3f,
                                        y + 0.5f,
                                        i.Value[0]._me._x + 0.3f,
                                        i.Value[0]._me._y + 0.5f,
                                        colorBlack,
                                        colorBlack);
                                }
                                // damage health
                                if (enemyMelees.ContainsKey(enemyId))
                                {
                                    enemyMelees[enemyId]._me._health -= damageR;
                                    if (enemyMelees[enemyId]._me._health <= 0)
                                    {
                                        wasKilled = true;
                                    }
                                }
                                else
                                if (enemyRangers.ContainsKey(enemyId))
                                {
                                    enemyRangers[enemyId]._me._health -= damageR;
                                    if (enemyRangers[enemyId]._me._health <= 0)
                                    {
                                        wasKilled = true;
                                    }
                                }
                                if (wasKilled)
                                {
                                    if (enemiesById.ContainsKey(enemyId))
                                    {
                                        DrawCenterCellTextSafe(enemiesById[enemyId].Position.X, enemiesById[enemyId].Position.Y, colorRed, "X", 16, DebugOptions.drawPlanedKill);
                                        enemiesById.Remove(enemyId);
                                    }
                                }
                            }
                        }
                    }
                }
                //if (deleteKeys.Count > 0)
                //    wasRemoved = true;
                foreach (var i in deleteKeys) // убираем таких парней
                {
                    myRangers.Remove(i);
                }
                //теперь вычеркиваем убитых и еще раз проверяем на наличие одной цели
                if (wasKilled)
                {
                    deleteKeys.Clear();
                    foreach (var i in enemyMelees)
                    {
                        if (i.Value._me._health <= 0)
                        {
                            deleteKeys.Add(i.Key);
                            foreach (var k in i.Value._targetsMyUnitsById)
                            {
                                if (myRangers.ContainsKey(k.Key))
                                {
                                    if (myRangers[k.Key].Contains(i.Value))
                                        myRangers[k.Key].Remove(i.Value);
                                }
                            }
                        }
                    }
                    foreach (var id in deleteKeys)
                    {
                        enemyMelees.Remove(id);
                    }
                    deleteKeys.Clear();
                    foreach (var i in enemyRangers)
                    {
                        if (i.Value._me._health <= 0)
                        {
                            deleteKeys.Add(i.Key);
                            foreach (var k in i.Value._targetsMyUnitsById)
                            {
                                if (myRangers.ContainsKey(k.Key))
                                {
                                    if (myRangers[k.Key].Contains(i.Value))
                                        myRangers[k.Key].Remove(i.Value);
                                }
                            }
                        }
                    }
                    foreach (var id in deleteKeys)
                    {
                        enemyRangers.Remove(id);
                    }
                }
            } while (wasKilled == true);
            #endregion

            #region максимизируем количество убийств

            //собираем первую группу
            if (myRangers.Count > 0)
            {
                List<int> attackers = new List<int>();
                List<int> targets = new List<int>();
                var num = myRangers.GetEnumerator();
                num.MoveNext();
                attackers.Add(num.Current.Key);
                bool wasAdded;
                int a = 0;
                int t = 0;
                do
                {
                    wasAdded = false;
                    for (; a < attackers.Count; a++)
                    {
                        foreach (var en in myRangers[attackers[a]])
                        {
                            if (targets.Contains(en._me._id) == false)
                            {
                                targets.Add(en._me._id);
                                wasAdded = true;
                            }
                        }
                    }
                    for (; t < targets.Count; t++)
                    {
                        if (enemyMelees.ContainsKey(targets[t]))
                        {
                            foreach (var me in enemyMelees[targets[t]]._targetsMyUnitsById)
                            {
                                if (myRangers.ContainsKey(me.Key) == true && attackers.Contains(me.Key) == false)
                                {
                                    attackers.Add(me.Key);
                                    wasAdded = true;
                                }
                            }
                        }
                        else if (enemyRangers.ContainsKey(targets[t]))
                        {
                            foreach (var me in enemyRangers[targets[t]]._targetsMyUnitsById)
                            {
                                if (myRangers.ContainsKey(me.Key) && attackers.Contains(me.Key) == false)
                                {
                                    attackers.Add(me.Key);
                                    wasAdded = true;
                                }
                            }
                        }
                    }
                } while (wasAdded == true);
                // получили группу
                int sizeA = attackers.Count;
                int sizeT = targets.Count;
                bool[,] arrayPair = new bool[sizeA, sizeT];
                for (int att = 0; att < sizeA; att++)
                {
                    for (int tar = 0; tar < sizeT; tar++)
                    {
                        arrayPair[att, tar] = false;
                    }
                }
                for (int i = 0; i < attackers.Count; i++)
                {
                    foreach (var en in myRangers[attackers[i]])
                    {
                        arrayPair[i, targets.IndexOf(en._me._id)] = true;
                    }
                }
                int[] targetsHealth = new int[sizeT];
                for (int i = 0; i < sizeT; i++)
                {
                    if (enemyMelees.ContainsKey(targets[i]))
                    {
                        targetsHealth[i] = System.Convert.ToInt32(System.Math.Ceiling(((float)enemyMelees[targets[i]]._me._health) / ((float)damageR)));
                    }
                    else if (enemyRangers.ContainsKey(targets[i]))
                    {
                        targetsHealth[i] = System.Convert.ToInt32(System.Math.Ceiling(((float)enemyRangers[targets[i]]._me._health) / ((float)damageR)));
                    }
                }

                // вариант 1 рукопашник на близкой дистанции

                if (targets.Count == 1)
                {
                    if (enemyMelees.ContainsKey(targets[0]))
                    {
                        if (attackers.Count * damageR >= enemyMelees[targets[0]]._me._health)
                        {
                            // fire all
                            foreach (var id in attackers)
                            {
                                int x = entityMemories[id].myEntity.Position.X;
                                int y = entityMemories[id].myEntity.Position.Y;
                                nextPositionMyUnitsMap[x][y] = id;
                                int enemyId = enemyMelees[targets[0]]._me._id;
                                entityMemories[id].OrderAttack(enemyId, null, true);
                                if (debugOptions[(int)DebugOptions.drawOptAttack])
                                {
                                    DrawLineOnce(
                                        entityMemories[id].myEntity.Position.X + 0.3f,
                                        entityMemories[id].myEntity.Position.Y + 0.5f,
                                        enemyMelees[targets[0]]._me._x + 0.3f,
                                        enemyMelees[targets[0]]._me._y + 0.5f,
                                        colorMagenta,
                                        colorMagenta);
                                }
                                enemyMelees[targets[0]]._me._health -= damageR;
                                if (enemyMelees[targets[0]]._me._health <= 0)
                                {
                                    if (enemiesById.ContainsKey(enemyId))
                                    {
                                        DrawCenterCellTextSafe(enemiesById[enemyId].Position.X, enemiesById[enemyId].Position.Y, colorRed, "X", 16, DebugOptions.drawPlanedKill);
                                        enemiesById.Remove(enemyId);
                                    }
                                }

                            }
                        }
                        else
                        {
                            foreach (var id in attackers)
                            {
                                if (myRangers[id][0]._targetsMyUnitsById[id]._dist <= 2)// retreat nearest
                                {
                                    entityMemories[id].OrderTryRetreat(true);
                                }
                                else // fire another
                                {
                                    int x = entityMemories[id].myEntity.Position.X;
                                    int y = entityMemories[id].myEntity.Position.Y;
                                    nextPositionMyUnitsMap[x][y] = id;
                                    entityMemories[id].OrderAttack(enemyMelees[targets[0]]._me._id, null, true);
                                    enemyMelees[targets[0]]._me._health -= damageR;
                                    if (debugOptions[(int)DebugOptions.drawOptAttack])
                                    {
                                        DrawLineOnce(
                                            entityMemories[id].myEntity.Position.X + 0.3f,
                                            entityMemories[id].myEntity.Position.Y + 0.5f,
                                            enemyMelees[targets[0]]._me._x + 0.3f,
                                            enemyMelees[targets[0]]._me._y + 0.5f,
                                            colorMagenta,
                                            colorMagenta);
                                    }
                                }
                            }
                            // remove attackers
                            foreach (var id in attackers)
                            {
                                myRangers.Remove(id);
                            }

                        }
                    }
                }
                else
                {

                    // вариант 2 врагов больше

                    List<int[]> attackVariants;// = new List<int[]>();

                    attackVariants = CalcMaxKillFromArray(sizeA, sizeT, arrayPair, targetsHealth);

                    //отдаем приказы по любому варианту
                    if (attackVariants.Count > 0)
                    {
                        int index = 0; // выбираем первый варинат
                        // исполняем вариант
                        for (int kk = 0; kk < sizeA; kk++)
                        {
                            int enemyId = targets[attackVariants[index][kk]];
                            int myUnitId = attackers[kk];
                            int x = entityMemories[myUnitId].myEntity.Position.X;
                            int y = entityMemories[myUnitId].myEntity.Position.Y;
                            nextPositionMyUnitsMap[x][y] = myUnitId;
                            entityMemories[myUnitId].OrderAttack(enemyId, null, true);
                            
                            bool isDead = false;
                            if (enemyMelees.ContainsKey(enemyId))
                            {
                                enemyMelees[enemyId]._me._health -= damageR;
                                if (enemyMelees[enemyId]._me._health <= 0)
                                    isDead = true;

                            }
                            else if (enemyRangers.ContainsKey(enemyId))
                            {
                                enemyRangers[enemyId]._me._health -= damageR;
                                if (enemyRangers[enemyId]._me._health <= 0)
                                    isDead = true;
                            }
                            if (enemiesById.ContainsKey(enemyId))
                            {
                                if (debugOptions[(int)DebugOptions.drawOptAttack])
                                {
                                    DrawLineOnce(
                                        x + 0.3f,
                                        y + 0.5f,
                                        enemiesById[enemyId].Position.X + 0.3f,
                                        enemiesById[enemyId].Position.Y + 0.5f,
                                        colorMagenta,
                                        colorMagenta);
                                }
                                if (isDead)
                                {
                                    DrawCenterCellTextSafe(enemiesById[enemyId].Position.X, enemiesById[enemyId].Position.Y, colorRed, "X", 16, DebugOptions.drawPlanedKill);
                                    enemiesById.Remove(enemyId);
                                }
                            }
                        }
                    }
                    else
                    {
                        // очищаем список, так как ничего не придумалось
                        ;
                    }
                    // очищаем список атаковавших
                    foreach (var id in attackers)
                    {
                        myRangers.Remove(id);
                    }
                    // очищаем список врагов
                    deleteKeys.Clear();
                    foreach (var i in enemyMelees)
                    {
                        if (i.Value._me._health <= 0)
                        {
                            deleteKeys.Add(i.Key);
                            foreach (var k in i.Value._targetsMyUnitsById)
                            {
                                if (myRangers.ContainsKey(k.Key))
                                {
                                    if (myRangers[k.Key].Contains(i.Value))
                                        myRangers[k.Key].Remove(i.Value);
                                }
                            }
                        }
                    }
                    foreach (var id in deleteKeys)
                    {
                        enemyMelees.Remove(id);
                    }
                    deleteKeys.Clear();
                    foreach (var i in enemyRangers)
                    {
                        if (i.Value._me._health <= 0)
                        {
                            deleteKeys.Add(i.Key);
                            foreach (var k in i.Value._targetsMyUnitsById)
                            {
                                if (myRangers.ContainsKey(k.Key))
                                {
                                    if (myRangers[k.Key].Contains(i.Value))
                                        myRangers[k.Key].Remove(i.Value);
                                }
                            }
                        }
                    }
                    foreach (var id in deleteKeys)
                    {
                        enemyRangers.Remove(id);
                    }
                }

            }
            #endregion
        }
        void OptimizeNearRangersMove(int distance)
        {
            List<int> pushedUnitId = new List<int>();
            CellWI[,] pathMap = new CellWI[mapSize, mapSize];
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    pathMap[x, y] = new CellWI();
                }
            }
            #region стартовые значения
            //стартовое значение, которое будем уменьшать
            int startWeight = mapSize * mapSize;
            int minWeight = startWeight - distance;
            int WInside = -1;
            int WBuilding = -2;
            int WEnemy = -3;
            int WResource = -4;
            int WDanger = -5;
            int WWarrior = -6;
            int WNextPosition = -7;
            int WDeniedUnit = -8;
            int WUnvisibleCell = -9;
            
            const int IBuilding = 1;
            const int IResource = 2;
            const int IMovedUnit = 3;
            const int INextPosition = 4;
            const int IFreeUnit = 5;
            const int IPushedUnit = 6;
            const int IOptimized = 7;
            const int IEnemy = 8;
            const int IWait = 9;
            const int IOnPosition = 10;
            #endregion

            #region определяем стартовые клетки
            //добавляем стартовые клетки поиска там где PotencAttack > 0
            List<int> myRangers = new List<int>();
            List<XYWeight> findCells = new List<XYWeight>();
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    if (potencAttackMap[x, y].min > 0)
                    {                        
                        bool canContinue = true;
                        pathMap[x, y].weight = startWeight;

                        int id = cellWithIdAny[x][y];
                        if (id >= 0)// occupied cell
                        {
                            if (entityMemories.ContainsKey(id))
                            {
                                if (entityMemories[id].optimized == false)
                                {
                                    EntityType type = entityMemories[id].myEntity.EntityType;
                                    if (type == EntityType.RangedUnit)
                                    {
                                        //myRangers.Add(id);
                                        ////canContinue = false; // поиск проходит сквозь этого парня
                                        ////pathMap[nx, ny].weight = WDeniedUnit;
                                        pathMap[x, y].index = IOnPosition;
                                        nextPositionMyUnitsMap[x][y] = id;
                                        bool[] availableTargetsType = FindAvailableTargetType(x, y, properties[type].Size, properties[type].Attack.Value.AttackRange);
                                        EntityType[] targetTypes;
                                        if (availableTargetsType[(int)EntityType.RangedUnit] == true)
                                            targetTypes = new EntityType[] { EntityType.RangedUnit };
                                        else if (availableTargetsType[(int)EntityType.MeleeUnit] == true)
                                            targetTypes = new EntityType[] { EntityType.RangedUnit, EntityType.MeleeUnit };
                                        else if (availableTargetsType[(int)EntityType.BuilderUnit] == true)
                                            targetTypes = new EntityType[] { EntityType.RangedUnit, EntityType.MeleeUnit, EntityType.BuilderUnit };
                                        else
                                            targetTypes = new EntityType[] { };

                                        AutoAttack autoAttack = new AutoAttack(0, targetTypes);
                                        entityMemories[id].OrderAttack(null, autoAttack, true);
                                    }
                                    else
                                    {
                                        if (properties[entityMemories[id].myEntity.EntityType].CanMove == false)//is my building
                                        {
                                            canContinue = false;
                                            pathMap[x, y].weight = WBuilding;
                                            pathMap[x, y].index = IBuilding;
                                        }
                                        else
                                        {
                                            //canContinue = false;
                                            pathMap[x, y].index = IFreeUnit;
                                        }
                                    }
                                }
                                else
                                {
                                    pathMap[x, y].index = IOptimized;
                                }
                            }
                            else if (enemiesById.ContainsKey(id))// enemy 
                            {
                                //canContinue = false;
                                //pathMap[nx, ny].weight = WDanger;
                                pathMap[x, y].index = IEnemy;
                            }
                            else // it is resource
                            {
                                //canContinue = false;
                                //pathMap[nx, ny].weight = WResource;
                                pathMap[x, y].index = IResource;
                            }
                        }

                        if (canContinue == true)
                        {
                            findCells.Add(new XYWeight(x, y, startWeight)); //  ищем от тех что стоит на границе
                        }
                    }


                }
            }
            #endregion

            // начинаем искать 
            for (int iter = 0; iter < findCells.Count; iter++)
            {
                int fx = findCells[iter].x;
                int fy = findCells[iter].y;
                int fw = findCells[iter].weight;
                int fi = findCells[iter].index;

                if (fw > minWeight)
                {
                    for (int jj = 0; jj < 4; jj++)
                    {
                        int nx = fx;
                        int ny = fy;
                        if (jj == 0) nx--;
                        if (jj == 1) ny--;
                        if (jj == 2) nx++;
                        if (jj == 3) ny++;

                        if (nx >= 0 && nx < mapSize && ny >= 0 && ny < mapSize) // все в границах карты
                        {
                            if (pathMap[nx, ny].weight == 0)
                            {
                                bool canContinue = true;

                                //if (potencAttackMap[nx, ny].min < 0) // опасная клетка // выстаскимваем из опасной зоны юнитов (когда они не атакуют
                                //{
                                //    canContinue = false;
                                //    pathMap[nx, ny].weight = WDanger;
                                //}
                                //else 
                                if (nextPositionMyUnitsMap[nx][ny] > 0) // проверка пустой позиции на следующий ход
                                {
                                    //canContinue = false; // проходим поиском сквозь движущиеся объекты
                                    //pathMap[nx, ny].weight = WNextPosition;
                                    pathMap[nx, ny].index = INextPosition;
                                }
                                else if (onceVisibleMap[nx][ny] == 0)
                                {
                                    canContinue = false;
                                    pathMap[nx, ny].weight = WUnvisibleCell;
                                }

                                if (canContinue == true)
                                {
                                    int id = cellWithIdAny[nx][ny];
                                    if (id >= 0)// occupied cell
                                    {
                                        if (entityMemories.ContainsKey(id))
                                        {
                                            if (entityMemories[id].optimized == false)
                                            {
                                                if (entityMemories[id].myEntity.EntityType == EntityType.RangedUnit)
                                                {
                                                    //myRangers.Add(id);
                                                    bool canMove = false;
                                                    switch (pathMap[fx,fy].index)
                                                    {
                                                        case IMovedUnit:
                                                        case IFreeUnit:
                                                        case 0:
                                                            canMove = true;
                                                            break;
                                                        case IResource:
                                                        case IOnPosition:
                                                        case IBuilding:
                                                        case INextPosition:
                                                        case IOptimized:
                                                        case IEnemy:
                                                        case IPushedUnit:
                                                        case IWait:
                                                            canMove = false;
                                                            break;
                                                        default:
                                                            throw new System.Exception("непонятный индекс");
                                                    }

                                                    if (canMove)
                                                    {
                                                        if (debugOptions[(int)DebugOptions.drawPotencAttackMove])
                                                        {
                                                            DrawLineOnce(nx + 0.6f, ny + 0.6f, fx + 0.6f, fy + 0.6f, colorMagenta, colorRed);
                                                        }

                                                        //canContinue = false; // поиск проходит сквозь этого парня
                                                        //pathMap[nx, ny].weight = WDeniedUnit;
                                                        pathMap[nx, ny].index = IMovedUnit;
                                                        nextPositionMyUnitsMap[fx][fy] = id;
                                                        fw--;
                                                        entityMemories[id].OrderMove(new Vec2Int(fx, fy), true, false, true);
                                                        int pushId = cellWithIdAny[fx][fy];
                                                        if (pushId > 0) // в принимающей клетке, кто-то есть
                                                        {
                                                            if (entityMemories.ContainsKey(pushId))
                                                                if (entityMemories[pushId].optimized == false)
                                                                    pushedUnitId.Add(pushId);
                                                        }
                                                        break;
                                                    }
                                                    else
                                                    {
                                                        pathMap[nx, ny].index = IWait;
                                                        canContinue = false;
                                                    }
                                                }
                                                else
                                                {
                                                    if (properties[entityMemories[id].myEntity.EntityType].CanMove == false)//is my building
                                                    {
                                                        //canContinue = false;
                                                        //pathMap[nx, ny].weight = WBuilding;
                                                        pathMap[nx, ny].index = IBuilding;
                                                    }
                                                    else
                                                    {
                                                        //canContinue = false;
                                                        pathMap[nx, ny].index = IFreeUnit;                                                        
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                pathMap[nx, ny].index = IOptimized;
                                            }
                                        }
                                        else if (enemiesById.ContainsKey(id))// enemy 
                                        {
                                            //canContinue = false;
                                            //pathMap[nx, ny].weight = WDanger;
                                            pathMap[nx, ny].index = IEnemy;
                                        }
                                        else // it is resource
                                        {
                                            //canContinue = false;
                                            //pathMap[nx, ny].weight = WResource;
                                            pathMap[nx, ny].index = IResource;
                                        }
                                    }
                                }

                                if (canContinue == true) // empty, safe cell or through free unit
                                {
                                    //add weight and findCell
                                    pathMap[nx, ny].weight = fw - 1;
                                    //pathMap[nx, ny].index = fi;
                                    if (fw > minWeight)
                                    {
                                        findCells.Add(new XYWeight(nx, ny, fw - 1, fi));

                                        if (debugOptions[(int)DebugOptions.drawPotencAttackPathfind])
                                        {
                                            DrawLineOnce(nx + 0.5f, ny + 0.5f, fx + 0.5f, fy + 0.5f, colorBlack, colorBlack);
                                        }
                                    }
                                }
                            }
                            
                            //можем не проверять уже занятые клетки, так как у нас волны распространяются по очереди 1-2-3-4 и т.д.
                        }
                    }
                    if (debugOptions[(int)DebugOptions.canDrawGetAction] && debugOptions[(int)DebugOptions.drawPotencAttackPathfind])
                    {
                        _debugInterface.Send(new DebugCommand.Flush());
                    }
                }
            }

            for (int i = 0; i < pushedUnitId.Count; i++)
            {
                int id = pushedUnitId[i];
                if (entityMemories.ContainsKey(id))
                {
                    if (true)//entityMemories[id].myEntity.EntityType == EntityType.BuilderUnit)
                    {
                        List<Vec2Int> pushCell = new List<Vec2Int>();
                        int sx = entityMemories[id].myEntity.Position.X;
                        int sy = entityMemories[id].myEntity.Position.Y;
                        for (int jj = 0; jj < 4; jj++)
                        {
                            int nx = sx;
                            int ny = sy;
                            if (jj == 0) nx--;
                            if (jj == 1) ny--;
                            if (jj == 2) nx++;
                            if (jj == 3) ny++;

                            if (nx >= 0 && nx < mapSize && ny >= 0 && ny < mapSize)
                            {
                                if (nextPositionMyUnitsMap[nx][ny] <= 0)
                                {
                                    if (cellWithIdOnlyBuilding[nx][ny] <= 0)
                                    {
                                        pushCell.Add(new Vec2Int(nx, ny));
                                    }
                                }
                            }
                        }

                        if (pushCell.Count > 0)
                        {
                            Vec2Int moveTo = pushCell[random.Next(pushCell.Count)];
                            entityMemories[id].OrderPushed(moveTo, false, false, true);
                            nextPositionMyUnitsMap[moveTo.X][moveTo.Y] = id;
                            if (debugOptions[(int)DebugOptions.drawOptRangerMove])
                            {
                                DrawLineOnce(sx + 0.55f, sy + 0.55f, moveTo.X + 0.55f, moveTo.Y + 0.55f, colorWhite, colorWhite);
                            }

                            int pushId = cellWithIdAny[moveTo.X][moveTo.Y];
                            if (pushId > 0) // в принимающей клетке, кто-то есть
                            {
                                if (entityMemories.ContainsKey(pushId))
                                    if (entityMemories[pushId].optimized == false)
                                        pushedUnitId.Add(pushId);
                            }
                        }
                        else
                        {
                            ;//бывает такое
                        }

                    }
                }

            }
        }
        void OptimizeSafeRangerAttack()
        {
            int damageR = properties[EntityType.RangedUnit].Attack.Value.Damage;
            int rangerSize = properties[EntityType.RangedUnit].Size;
            List<int> baseSelectMyRangersId = new List<int>();
            foreach (var en in entityMemories)
            {
                if (en.Value.myEntity.EntityType == EntityType.RangedUnit)
                {
                    if (en.Value.optimized == false)
                    {
                        baseSelectMyRangersId.Add(en.Key);
                    }
                }
            }

            List<List<EntityType>> enemyTypesRepeater = new List<List<EntityType>>();
            enemyTypesRepeater.Add(new List<EntityType>(new EntityType[]{ EntityType.BuilderUnit })); 
            enemyTypesRepeater.Add(new List<EntityType>(new EntityType[]{ EntityType.BuilderBase, EntityType.MeleeBase, EntityType.RangedBase }));
            enemyTypesRepeater.Add(new List<EntityType>(new EntityType[] { EntityType.House }));
            //typesRepeater.Add(new List<EntityType>(new EntityType[] { EntityType.R }));

            foreach (var enemyTypeList in enemyTypesRepeater)
            {
                #region составляем список врагов
                Dictionary<int, EnemyToOpt> enemyies = new Dictionary<int, EnemyToOpt>();                
                foreach (var en in enemiesById)
                {
                    if (enemyTypeList.Contains(en.Value.EntityType))
                    {
                        // enemyRangersId.Add(en.Key);
                        enemyies.Add(en.Key, new EnemyToOpt(en.Key, en.Value.EntityType, en.Value.Health, en.Value.Position.X, en.Value.Position.Y));
                    }
                }
                #endregion
                #region составляем список наших свободных стрелков
                Dictionary<int, List<EnemyToOpt>> myRangers = new Dictionary<int, List<EnemyToOpt>>();
                for (int i = 0; i < baseSelectMyRangersId.Count;)
                {
                    EntityMemory memory = entityMemories[baseSelectMyRangersId[i]];
                    if (memory.optimized == false)
                    {
                        int x = memory.myEntity.Position.X;
                        int y = memory.myEntity.Position.Y;
                        foreach (var enemyType in enemyTypeList)
                        {
                            int value = potencTarget5Map[x, y][enemyType];
                            if (value > 0)
                            {
                                myRangers.Add(baseSelectMyRangersId[i], new List<EnemyToOpt>());
                                break;
                            }
                        }
                        i++;
                    }
                    else
                    {
                        baseSelectMyRangersId.RemoveAt(i);
                    }
                }
                #endregion
                #region собираем пары всех кто на дистанции до 5 включительно
                foreach (var my in myRangers)
                {
                    int x1 = entityMemories[my.Key].myEntity.Position.X;
                    int y1 = entityMemories[my.Key].myEntity.Position.Y;

                    foreach (var en in enemyies)
                    {
                        int x2 = enemiesById[en.Key].Position.X;
                        int y2 = enemiesById[en.Key].Position.Y;
                        //int dist = Abs(x1 - x2) + Abs(y1 - y2);//!!! неправильно считааем дистанцию!
                        
                        int dist = Distance(x1, y1, rangerSize, x2, y2, properties[enemiesById[en.Key].EntityType].Size); // 1 
                        if (dist <= 5)
                        {
                            my.Value.Add(en.Value);
                            en.Value._me._dist = dist;
                            en.Value.Add(new Target(my.Key, enemiesById[en.Key].EntityType, entityMemories[my.Key].myEntity.Health, x1, y1, dist));
                        }
                    }
                }
                #endregion
                #region чистим списки кто остался с пустыми парами
                List<int> deleteKeys = new List<int>();
                foreach (var i in myRangers)
                {
                    if (i.Value.Count == 0)
                        deleteKeys.Add(i.Key);
                }
                foreach (var i in deleteKeys)
                {
                    myRangers.Remove(i);
                }
                deleteKeys.Clear();
                foreach (var i in enemyies)
                {
                    if (i.Value.Count == 0)
                        deleteKeys.Add(i.Key);
                }
                foreach (var i in deleteKeys)
                {
                    enemyies.Remove(i);
                }
                #endregion
                #region определяем действия тех, кто может стрелять только в одну цель
                bool wasKilled;
                do
                {
                    wasKilled = false;
                    deleteKeys.Clear();
                    foreach (var i in myRangers)
                    {
                        if (i.Value.Count == 1)
                        {
                            EnemyToOpt target = i.Value[0];

                            if (target._me._health > 0)
                            {
                                // remove my ranger, he do that he can
                                deleteKeys.Add(i.Key);
                                // create order
                                int x = entityMemories[i.Key].myEntity.Position.X;
                                int y = entityMemories[i.Key].myEntity.Position.Y;
                                nextPositionMyUnitsMap[x][y] = i.Key; // stop on position
                                int enemyId = target._me._id;
                                entityMemories[i.Key].OrderAttack(enemyId, null, true);
                                // draw attack line
                                if (debugOptions[(int)DebugOptions.drawOptAttack])
                                {
                                    DrawLineOnce(
                                        x + 0.3f,
                                        y + 0.5f,
                                        i.Value[0]._me._x + 0.3f,
                                        i.Value[0]._me._y + 0.5f,
                                        colorBlack,
                                        colorBlack);
                                }
                                // damage health
                                if (enemyies.ContainsKey(enemyId))
                                {
                                    enemyies[enemyId]._me._health -= damageR;
                                    if (enemyies[enemyId]._me._health <= 0)
                                    {
                                        wasKilled = true;
                                        if (enemiesById.ContainsKey(enemyId))
                                        {
                                            DrawCenterCellTextSafe(enemiesById[enemyId].Position.X, enemiesById[enemyId].Position.Y, colorRed, "X", 16, DebugOptions.drawPlanedKill);
                                            enemiesById.Remove(enemyId);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    foreach (var i in deleteKeys) // убираем отстрелявшихся таких парней
                    {
                        myRangers.Remove(i);
                    }
                    if (wasKilled)//теперь вычеркиваем убитых и еще раз проверяем на наличие одной цели
                    {
                        deleteKeys.Clear();
                        foreach (var i in enemyies)
                        {
                            if (i.Value._me._health <= 0)
                            {
                                deleteKeys.Add(i.Key);
                                foreach (var k in i.Value._targetsMyUnitsById)
                                {
                                    if (myRangers.ContainsKey(k.Key))
                                    {
                                        if (myRangers[k.Key].Contains(i.Value))
                                            myRangers[k.Key].Remove(i.Value);
                                    }
                                }
                            }
                        }
                        foreach (var id in deleteKeys)
                        {
                            enemyies.Remove(id);
                        }
                    }
                } while (wasKilled == true);
                #endregion
                #region максимизируем количество убийств
                //собираем первую группу
                if (myRangers.Count > 0)
                {
                    List<int> attackers = new List<int>();
                    List<int> targets = new List<int>();
                    var num = myRangers.GetEnumerator();
                    num.MoveNext();
                    attackers.Add(num.Current.Key); // добавляем первого из оставшихся стрелков и смотрим с кем он связан
                    bool wasAdded;
                    int a = 0;
                    int t = 0;
                    // собираем группу стрелков и целей, которые могут стрелять друг в друга
                    do
                    {
                        wasAdded = false;
                        for (; a < attackers.Count; a++)
                        {
                            foreach (var en in myRangers[attackers[a]])
                            {
                                if (targets.Contains(en._me._id) == false)
                                {
                                    targets.Add(en._me._id);
                                    wasAdded = true;
                                }
                            }
                        }
                        for (; t < targets.Count; t++)
                        {
                            if (enemyies.ContainsKey(targets[t]))
                            {
                                foreach (var me in enemyies[targets[t]]._targetsMyUnitsById)
                                {
                                    if (myRangers.ContainsKey(me.Key) == true && attackers.Contains(me.Key) == false)
                                    {
                                        attackers.Add(me.Key);
                                        wasAdded = true;
                                    }
                                }
                            }
                        }
                    } while (wasAdded == true);
                    // получили группу
                    int sizeA = attackers.Count;
                    int sizeT = targets.Count;
                    bool[,] arrayPair = new bool[sizeA, sizeT];
                    for (int att = 0; att < sizeA; att++)
                    {
                        for (int tar = 0; tar < sizeT; tar++)
                        {
                            arrayPair[att, tar] = false;
                        }
                    }
                    for (int i = 0; i < attackers.Count; i++)
                    {
                        foreach (var en in myRangers[attackers[i]])
                        {
                            arrayPair[i, targets.IndexOf(en._me._id)] = true;
                        }
                    }
                    int[] targetsHealth = new int[sizeT];
                    for (int i = 0; i < sizeT; i++)
                    {
                        targetsHealth[i] = System.Convert.ToInt32(System.Math.Ceiling(((float)enemyies[targets[i]]._me._health) / ((float)damageR)));
                    }

                    // вариант 2 врагов больше
                    List<int[]> attackVariants;// = new List<int[]>();

                    attackVariants = CalcMaxKillFromArray(sizeA, sizeT, arrayPair, targetsHealth);

                    //отдаем приказы по любому варианту
                    if (attackVariants.Count > 0)
                    {
                        int index = 0; // выбираем первый варинат
                                       // исполняем вариант
                        for (int kk = 0; kk < sizeA; kk++)
                        {
                            int enemyId = targets[attackVariants[index][kk]];

                            int myUnitId = attackers[kk];
                            int x = entityMemories[myUnitId].myEntity.Position.X;
                            int y = entityMemories[myUnitId].myEntity.Position.Y;
                            nextPositionMyUnitsMap[x][y] = myUnitId;
                            entityMemories[myUnitId].OrderAttack(enemyId, null, true);
                            if (debugOptions[(int)DebugOptions.drawOptAttack])
                            {
                                DrawLineOnce(
                                    x + 0.3f,
                                    y + 0.5f,
                                    enemiesById[enemyId].Position.X + 0.3f,
                                    enemiesById[enemyId].Position.Y + 0.5f,
                                    colorMagenta,
                                    colorMagenta);
                            }
                            enemyies[enemyId]._me._health -= damageR;
                            if (enemyies[enemyId]._me._health <= 0)
                            {
                                DrawCenterCellTextSafe(enemiesById[enemyId].Position.X, enemiesById[enemyId].Position.Y, colorRed, "X", 16, DebugOptions.drawPlanedKill);
                                enemiesById.Remove(enemyId);
                            }
                        }
                    }
                    else
                    {
                        // очищаем список, так как ничего не придумалось
                        ;
                    }
                    // очищаем список атаковавших
                    foreach (var id in attackers)
                    {
                        myRangers.Remove(id);
                    }
                    // очищаем список врагов
                    deleteKeys.Clear();
                    foreach (var i in enemyies)
                    {
                        if (i.Value._me._health <= 0)
                        {
                            deleteKeys.Add(i.Key);
                            foreach (var k in i.Value._targetsMyUnitsById)
                            {
                                if (myRangers.ContainsKey(k.Key))
                                {
                                    if (myRangers[k.Key].Contains(i.Value))
                                        myRangers[k.Key].Remove(i.Value);
                                }
                            }
                        }
                    }
                    foreach (var id in deleteKeys)
                    {
                        enemyies.Remove(id);
                    }
                }
                #endregion
            }
       
        }
        void OptimizeWarriorsMove()
        {

            #region стартовые значения
            List<int> pushedUnitId = new List<int>();
            CellWI[,] pathMap = new CellWI[mapSize, mapSize];
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    pathMap[x, y] = new CellWI();
                }
            }

            //стартовое значение, которое будем уменьшать
            int startWeight = mapSize * mapSize;
            //int minWeight = startWeight - maxHealth;
            // 
            //const  int WInside = -1;
            const int WmyBuilding = -2;
            const int WEnemy = 3; // поиск проходит сквозь вражеских юнитов и здания
            const int WResource = -4;
            //const int WDanger = -5;
            const int WmyUnit = -6;
            const int WNextPosition = 7; // поиск проходит сквозь мои позиции пути, но туда нельзя стать
            //const int WoptimizedUnit = 8;
            //const int WUnvisibleCell = -9;

            List<EntityType> validTargetTypes = new List<EntityType>();
            validTargetTypes.Add(EntityType.BuilderUnit);
            validTargetTypes.Add(EntityType.RangedUnit);
            validTargetTypes.Add(EntityType.MeleeUnit);
            //validTargetTypes.Add(EntityType.Turret);
            validTargetTypes.Add(EntityType.House);
            validTargetTypes.Add(EntityType.BuilderBase);
            validTargetTypes.Add(EntityType.MeleeBase);
            validTargetTypes.Add(EntityType.RangedBase);
            //validTargetTypes.Add(EntityType.Wall);
            //validTargetTypes.Add(EntityType.Resource);
            #endregion

            #region определяем стартовые клетки
            List<XYWeight> findCells = new List<XYWeight>();
            for (int x = 0; x < mapSize; x++) // обозначаем все клетки с которых можно атаковать противника
            {
                for (int y = 0; y < mapSize; y++)
                {
                    int sum = 0;
                    foreach (var t in validTargetTypes)
                        sum += potencTarget5Map[x, y][t];
                    pathMap[x, y].index = sum;
                }
            }

            for (int x = 0; x < mapSize; x++) // ищем граничные клетки, чтобы от них начать поиск
            {
                for (int y = 0; y < mapSize; y++)
                {
                    if (pathMap[x, y].index > 0)
                    {
                        if (cellWithIdOnlyBuilding[x][y] == -1) // Это не здание
                        {
                            int sumFreeCells = 0;
                            for (int h = 0; h < 4; h++)
                            {
                                int fx = x;
                                int fy = y;
                                if (h == 0) fx++;
                                else if (h == 1) fx--;
                                else if (h == 2) fy++;
                                else if (h == 3) fy--;
                                if (fx >= 0 && fx < mapSize && fy >= 0 && fy < mapSize)
                                {
                                    if (cellWithIdOnlyBuilding[fx][fy] == -1)
                                        if (pathMap[fx, fy].index == 0)
                                            sumFreeCells++;
                                }
                            }

                            if (sumFreeCells > 0) // у этой клетки есть сосед без атаки, надо отсюда начинать поиск
                            {
                                findCells.Add(new XYWeight(x, y, startWeight));
                                pathMap[x, y].weight = startWeight;
                                if (debugOptions[(int)DebugOptions.canDrawGetAction] && debugOptions[(int)DebugOptions.drawOptRangerPathfind])
                                {
                                    DrawCenterCellText(x, y, colorRed, 0, 16);
                                }
                            }
                        }
                    }
                }
            }
            #endregion

            // идем в угол противника если нет целей
            if (findCells.Count == 0)
            {
                if (isFinal)
                {
                    findCells.Add(new XYWeight(73, 73, startWeight));
                }
                else if (fogOfWar)
                {
                    if (_playerView.CurrentTick < 500)
                        findCells.Add(new XYWeight(73, 7, startWeight));
                    else if (_playerView.CurrentTick < 750)
                        findCells.Add(new XYWeight(73, 73, startWeight));
                    else findCells.Add(new XYWeight(7, 73, startWeight));
                }
            }

            int debugStep = startWeight;
            // начинаем искать людей
            for (int iter = 0; iter < findCells.Count; iter++)
            {
                int fx = findCells[iter].x;
                int fy = findCells[iter].y;
                int fw = findCells[iter].weight;
                int fi = findCells[iter].index;

                for (int jj = 0; jj < 4; jj++)
                {
                    int nx = fx;
                    int ny = fy;
                    if (jj == 0) nx--;
                    if (jj == 1) ny--;
                    if (jj == 2) nx++;
                    if (jj == 3) ny++;

                    if (nx >= 0 && nx < mapSize && ny >= 0 && ny < mapSize) // все в границах карты
                    {
                        if (pathMap[nx, ny].index == 0 && pathMap[nx, ny].weight >= 0 && pathMap[nx, ny].weight < fw - 2)
                        {
                            bool canContinue = true;

                            //var dCell = enemyDangerCells[nx][ny];
                            //if (dCell.meleesAim + dCell.rangersAim + dCell.turretsAim > 0) // проверка опасной зоны
                            //{
                            //    canContinue = false;
                            //    pathMap[nx, ny].weight = WDanger;
                            //}
                            //else if (dCell.meleesWarning + dCell.rangersWarning > 0)
                            //{
                            //    canContinue = false;
                            //    pathMap[nx, ny].weight = WDanger;
                            //}
                            //else 
                            if (nextPositionMyUnitsMap[nx][ny] > 0) // проверка пустой позиции на следующий ход
                            {
                                //canContinue = false;
                                pathMap[nx, ny].weight = WNextPosition;
                            } else if(resourceMemoryMap[nx][ny] > 0) // не проходит поиск через ресурсы
                            {
                                canContinue = false;
                                pathMap[nx, ny].weight = WResource;
                            }
                            //else if (onceVisibleMap[nx][ny] == 0)
                            //{
                            //    canContinue = false;
                            //    pathMap[nx, ny].weight = WUnvisibleCell;
                            //}

                            if (canContinue == true)
                            {
                                int id = cellWithIdAny[nx][ny];
                                if (id >= 0)// occupied cell
                                {
                                    if (entityMemories.ContainsKey(id))
                                    {
                                        if (entityMemories[id].myEntity.EntityType == EntityType.RangedUnit)
                                        {
                                            if (entityMemories[id].optimized == false)
                                            {
                                                //canContinue = false; поиск распространяется свкозь моих
                                                
                                                //pathMap[nx, ny].weight = WoptimizedUnit;
                                                if (nextPositionMyUnitsMap[fx][fy] <= 0) // принимабющая клетка пустая, можно в нее идти
                                                {
                                                    if ( debugOptions[(int)DebugOptions.drawOptRangerMove])
                                                    {
                                                        DrawLineOnce(nx + 0.45f, ny + 0.45f, fx + 0.45f, fy + 0.45f, colorGreen, colorGreen);
                                                    }
                                                    nextPositionMyUnitsMap[fx][fy] = id;
                                                    entityMemories[id].OrderMove(new Vec2Int(fx, fy), false, false, true);
                                                    int pushId = cellWithIdAny[fx][fy];
                                                    if (pushId > 0) // в принимающей клетке, кто-то есть
                                                    {
                                                        if (entityMemories.ContainsKey(pushId))
                                                            if (entityMemories[pushId].optimized == false)
                                                                pushedUnitId.Add(pushId);
                                                    }
                                                } else
                                                {
                                                    canContinue = false;
                                                }
                                            }
                                            // все оптимизированные юниты придаставлены в NextPosition
                                            //else
                                            //{
                                            //    canContinue = false;
                                            //    pathMap[nx, ny].weight = WoptimizedUnit;
                                            //}

                                        }
                                        else
                                        {
                                            if (properties[entityMemories[id].myEntity.EntityType].CanMove == false)//is my building
                                            {
                                                canContinue = false;
                                                pathMap[nx, ny].weight = WmyBuilding;
                                            }
                                            // ищем сквозь моих юнитов
                                            //else
                                            //{
                                            //    canContinue = false;
                                            //    pathMap[nx, ny].weight = WmyUnit;
                                            //}
                                        }
                                    }
                                    else if (enemiesById.ContainsKey(id))// enemy 
                                    {
                                        //canContinue = false;
                                        //pathMap[nx, ny].weight = WDanger;
                                    }
                                    // наличие ресурса проверено ранее по карте памяти ресурсов
                                    //else // it is resource
                                    //{
                                    //    canContinue = false;
                                    //    pathMap[nx, ny].weight = WResource;
                                    //}
                                }
                            }

                            if (canContinue == true) // empty, safe cell or through free unit
                            {
                                //add weight and findCell
                                pathMap[nx, ny].weight = fw - 1;
                                //pathMap[nx, ny].index = fi;
                               
                                findCells.Add(new XYWeight(nx, ny, fw - 1, fi));
                                if (debugOptions[(int)DebugOptions.canDrawGetAction] && debugOptions[(int)DebugOptions.drawOptRangerPathfind])
                                {
                                    DrawCenterCellText(nx, ny, colorRed, startWeight - fw + 1, 16);
                                    //DrawLineOnce(nx + 0.5f, ny + 0.5f, fx + 0.5f, fy + 0.5f, colorMagenta, colorMagenta);
                                }
                         
                            }
                        }                        
                        //можем не проверять уже занятые клетки, так как у нас волны распространяются по очереди 1-2-3-4 и т.д.
                    }
                }
                //if (debugOptions[(int)DebugOptions.canDrawGetAction] && debugOptions[(int)DebugOptions.drawOptRangerPathfind])
                //{
                //    if (debugStep != fw)
                //    {
                //        debugStep = fw;
                //        _debugInterface.Send(new DebugCommand.Flush());
                //        ;
                //    }                    
                //}
            }

            for (int i = 0; i < pushedUnitId.Count; i++)
            {
                int id = pushedUnitId[i];
                if (entityMemories.ContainsKey(id))
                {
                    if (true)//entityMemories[id].myEntity.EntityType == EntityType.BuilderUnit)
                    {
                        List<Vec2Int> pushCell = new List<Vec2Int>();
                        int sx = entityMemories[id].myEntity.Position.X;
                        int sy = entityMemories[id].myEntity.Position.Y;
                        for (int jj = 0; jj < 4; jj++)
                        {
                            int nx = sx;
                            int ny = sy;
                            if (jj == 0) nx--;
                            if (jj == 1) ny--;
                            if (jj == 2) nx++;
                            if (jj == 3) ny++;

                            if (nx >= 0 && nx < mapSize && ny >= 0 && ny < mapSize)
                            {
                                if (nextPositionMyUnitsMap[nx][ny] <= 0)
                                {
                                    if(cellWithIdOnlyBuilding[nx][ny] <= 0)
                                    {
                                        pushCell.Add(new Vec2Int(nx, ny));
                                    }
                                }
                            }
                        }

                        if (pushCell.Count > 0)
                        {
                            Vec2Int moveTo = pushCell[random.Next(pushCell.Count)];
                            entityMemories[id].OrderPushed(moveTo, false, false, true);
                            nextPositionMyUnitsMap[moveTo.X][moveTo.Y] = id;
                            if (debugOptions[(int)DebugOptions.drawOptRangerMove])
                            {
                                DrawLineOnce(sx + 0.55f, sy + 0.55f, moveTo.X + 0.55f, moveTo.Y + 0.55f, colorWhite, colorWhite);
                            }

                            int pushId = cellWithIdAny[moveTo.X][moveTo.Y];
                            if (pushId > 0) // в принимающей клетке, кто-то есть
                            {
                                if (entityMemories.ContainsKey(pushId))
                                    if (entityMemories[pushId].optimized == false)
                                        pushedUnitId.Add(pushId);
                            }
                        } else
                        {
                            ;
                        }

                    }
                }

            }
        }
        #region Order statistics
        float drawOrderStatisticX;
        float drawOrderStatisticY;
        const float drawOrderStatisticStepX = 30f;
        const float drawOrderStatisticStepY = 16f;
        const float drawOrderStatisticStartX = 50f;
        const float drawOrderStatisticStartY = 90f;
        EntityOrders[] debugBuilderOrdersArray = new EntityOrders[] {
            EntityOrders.buildNow, EntityOrders.buildGo, EntityOrders.repairGo, EntityOrders.tryRetreat, EntityOrders.canRetreat,
            EntityOrders.attack, EntityOrders.moveAndAttack, EntityOrders.collect, EntityOrders.move, EntityOrders.pushed };
        EntityOrders[] debugRangerOrdersArray = new EntityOrders[] {
            EntityOrders.none, EntityOrders.tryRetreat, EntityOrders.canRetreat,
            EntityOrders.attack, EntityOrders.moveAndAttack, EntityOrders.collect, EntityOrders.move, EntityOrders.pushed };
        void DrawOrderStatisticInit()
        {
            if (debugOptions[(int)DebugOptions.canDrawGetAction] == true && debugOptions[(int)DebugOptions.drawOrderStatistics] == true)
            {
                drawOrderStatisticX = drawOrderStatisticStartX;
                drawOrderStatisticY = _debugInterface.GetState().WindowSize.Y - drawOrderStatisticStartY;

                float x = drawOrderStatisticX;
                float y = drawOrderStatisticY;
                int textSize = 16;
                _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(new ColoredVertex(null, new Vec2Float(x, y), colorGreen), "Builders", 1f, textSize)));
                foreach (var ord in debugBuilderOrdersArray)
                {
                    x += drawOrderStatisticStepX;
                    string text = "";
                    switch (ord)
                    {
                        case EntityOrders.attack: text = "At"; break;
                        case EntityOrders.moveAndAttack: text = "AtMv"; break;
                        case EntityOrders.buildGo: text = "bldG"; break;
                        case EntityOrders.buildNow: text = "bldN"; break;
                        case EntityOrders.cancelAll: text = "cAll"; break;
                        case EntityOrders.canRetreat: text = "canR"; break;
                        case EntityOrders.collect: text = "coll"; break;
                        case EntityOrders.move: text = "move"; break;
                        case EntityOrders.pushed: text = "pshd"; break;
                        case EntityOrders.none: text = "none"; break;
                        case EntityOrders.repairGo: text = "rprG"; break;
                        case EntityOrders.spawnUnit: text = "spUn"; break;
                        case EntityOrders.tryRetreat: text = "tryR"; break;
                    }

                    ColoredVertex position = new ColoredVertex(null, new Vec2Float(x, y), colorWhite);
                    _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, text, 0.5f, textSize)));
                }
                x += drawOrderStatisticStepX;
                _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(new ColoredVertex(null, new Vec2Float(x, y), colorWhite), "other", 0.5f, textSize)));

                x += drawOrderStatisticStepX;
                _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(new ColoredVertex(null, new Vec2Float(x, y), colorRed), "Rngr", 0.5f, textSize)));
                foreach (var ord in debugRangerOrdersArray)
                {
                    x += drawOrderStatisticStepX;
                    string text = "";
                    switch (ord)
                    {
                        case EntityOrders.attack: text = "At"; break;
                        case EntityOrders.moveAndAttack: text = "AtMv"; break;
                        case EntityOrders.buildGo: text = "bldG"; break;
                        case EntityOrders.buildNow: text = "bldN"; break;
                        case EntityOrders.cancelAll: text = "cAll"; break;
                        case EntityOrders.canRetreat: text = "canR"; break;
                        case EntityOrders.collect: text = "coll"; break;
                        case EntityOrders.move: text = "move"; break;
                        case EntityOrders.none: text = "none"; break;
                        case EntityOrders.pushed: text = "pshd"; break;
                        case EntityOrders.repairGo: text = "rprG"; break;
                        case EntityOrders.spawnUnit: text = "spUn"; break;
                        case EntityOrders.tryRetreat: text = "tryR"; break;
                    }

                    ColoredVertex position = new ColoredVertex(null, new Vec2Float(x, y), colorWhite);
                    _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, text, 0.5f, textSize)));
                }
                x += drawOrderStatisticStepX;
                _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(new ColoredVertex(null, new Vec2Float(x, y), colorWhite), "other", 0.5f, textSize)));

                drawOrderStatisticY -= drawOrderStatisticStepY;
            }
        }
        void DrawOrderStatistic(string text)
        {
            if (debugOptions[(int)DebugOptions.canDrawGetAction] == true && debugOptions[(int)DebugOptions.drawOrderStatistics] == true)
            {
                Dictionary<EntityOrders, int> orderStatBuildersNo = new Dictionary<EntityOrders, int>();
                Dictionary<EntityOrders, int> orderStatBuildersOpt = new Dictionary<EntityOrders, int>();
                Dictionary<EntityOrders, int> orderStatRangersNo = new Dictionary<EntityOrders, int>();
                Dictionary<EntityOrders, int> orderStatRangersOpt = new Dictionary<EntityOrders, int>();
                // collect statistics
                foreach (var en in entityMemories)
                {
                    bool correct = false;
                    Dictionary<EntityOrders, int> orderStat = null;
                    if (en.Value.optimized)
                    {
                        if (en.Value.myEntity.EntityType == EntityType.BuilderUnit)
                        {
                            correct = true;
                            orderStat = orderStatBuildersOpt;
                        }
                        else if (en.Value.myEntity.EntityType == EntityType.RangedUnit)
                        {
                            correct = true;
                            orderStat = orderStatRangersOpt;
                        }
                    }
                    else
                    {
                        if (en.Value.myEntity.EntityType == EntityType.BuilderUnit)
                        {
                            correct = true;
                            orderStat = orderStatBuildersNo;
                        }
                        else if (en.Value.myEntity.EntityType == EntityType.RangedUnit)
                        {
                            correct = true;
                            orderStat = orderStatRangersNo;
                        }
                    }

                    if (correct)
                    {
                        if (orderStat.ContainsKey(en.Value.order))
                        {
                            orderStat[en.Value.order]++;
                        }
                        else
                        {
                            orderStat.Add(en.Value.order, 1);
                        }
                    }
                }

                float x = drawOrderStatisticX;
                float y = drawOrderStatisticY;
                int textSize = 16;
                // draw ticket
                ColoredVertex position = new ColoredVertex(null, new Vec2Float(x, y), colorWhite);
                _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, text, 1f, textSize)));
                #region draw statistics builders

                int otherNo = 0;
                int otherOpt = 0;
                foreach (var or in orderStatBuildersNo)
                    otherNo += or.Value;
                foreach (var or in orderStatBuildersOpt)
                    otherOpt += or.Value;
                // { none, spawnUnit, buildNow, buildGo, repairGo, tryRetreat, canRetreat, attack, attackAndMove, collect, move, cancelAll }

                //float dx = 30f;
                foreach (var order in debugBuilderOrdersArray)
                {
                    int valueNo = orderStatBuildersNo.ContainsKey(order) ? orderStatBuildersNo[order] : 0;
                    int valueOpt = orderStatBuildersOpt.ContainsKey(order) ? orderStatBuildersOpt[order] : 0;
                    DrawStatPair(ref x, y, valueNo, valueOpt, ref otherNo, ref otherOpt, textSize);
                }                   

                DrawStatPair(ref x, y, otherNo, otherOpt, ref otherNo, ref otherOpt, textSize);
                #endregion

                #region draw statistics rangers
                x += drawOrderStatisticStepX;
                otherNo = 0;
                otherOpt = 0;
                foreach (var or in orderStatRangersNo)
                    otherNo += or.Value;
                foreach (var or in orderStatRangersOpt)
                    otherOpt += or.Value;

                //float dx = 30f;
                foreach (var order in debugRangerOrdersArray)
                {
                    int valueNo = orderStatRangersNo.ContainsKey(order) ? orderStatRangersNo[order] : 0;
                    int valueOpt = orderStatRangersOpt.ContainsKey(order) ? orderStatRangersOpt[order] : 0;
                    DrawStatPair(ref x, y, valueNo, valueOpt, ref otherNo, ref otherOpt, textSize);
                }

                DrawStatPair(ref x, y, otherNo, otherOpt, ref otherNo, ref otherOpt, textSize);
                #endregion

                drawOrderStatisticY -= drawOrderStatisticStepY;
                _debugInterface.Send(new DebugCommand.Flush());
            }
        }
        void DrawStatPair(ref float x, float y, int value1, int value2, ref int other1, ref int other2, int textSize)
        {
            x += drawOrderStatisticStepX;
            ColoredVertex position = new ColoredVertex(null, new Vec2Float(x, y), colorRed);
            if (value1 != 0)
                _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, value1.ToString(), 1f, textSize)));
            position.Color = colorGreen; // = new ColoredVertex(null, new Vec2Float(x, y), colorGreen);
            if (value2 != 0)
                _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, value2.ToString(), 0, textSize)));
            other1 -= value1;
            other2 -= value2;
        }
        #endregion
        void DrawDeadCellMap()
        {
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    if (cellWithIdOnlyBuilding[x][y] == -1)
                    {
                        int value = deadEndMap[x, y];
                        if (value > 0)
                        {
                            ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y+0.2f), new Vec2Float(0, 0), colorRed);
                            _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, value.ToString(), 0.5f, 16)));
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Определяет самые эффективные способы распределения целей, чтобы поразить максимум целей
        /// </summary>
        /// <param name="sizeA">количество стрелков</param>
        /// <param name="sizeT">количество целей</param>
        /// <param name="arrayPair">таблица [стрелки, цели], истина если стрелок может попасть в цель под этим номером</param>
        /// <param name="targetsHealth">Жизни у целей, 1 выстрел 1 жизнь</param>
        /// <returns>возвращает лист с массивом int[sizeA] где стрелкам по порядку укзаны цели, присутствуют только варианыт с максимальным убийством целей</returns>
        List<int[]> CalcMaxKillFromArray(int sizeA, int sizeT, bool[,] arrayPair, int[] targetsHealth)
        {
            List<int[]> variants = new List<int[]>(sizeT);
            int count = 0;
            // собираем все варианты
            for (int a = 0; a < sizeA; a++)
            {
                count = variants.Count;
                for (int t = 0; t < sizeT; t++)
                {
                    if (arrayPair[a, t] == true)
                    {
                        if (a == 0)
                        {
                            int[] v = new int[sizeA]; // создаем первые варианты
                            v[a] = t;
                            variants.Add(v);
                        }
                        else
                        {
                            for (int i = 0; i < count; i++)//перебераем все варианты для а стрелков
                            {
                                int[] v = new int[sizeA]; // копируем вариант
                                variants[i].CopyTo(v, 0);

                                int sum = 0;// не учитываем текущий выбор
                                for (int k = 0; k < a; k++)
                                {
                                    if (v[k] == t)
                                        sum++;
                                }
                                if (sum < targetsHealth[t])
                                {
                                    v[a] = t; // учитываем наш выбор
                                    variants.Add(v); // добавляем вариант
                                }
                            }
                        }
                    }
                }
                variants.RemoveRange(0, count); // убираем варианты с меньшим количеством стрелков
            }
            // считаем количество убйиств для каждого варианта
            List<int> killsArray = new List<int>(); // параллельный массив, который содержит количество убийств в варианте
            for (int i = 0; i < variants.Count; i++)
            {
                int[] damage = new int[sizeT];
                foreach (var t in variants[i])
                {
                    damage[t]++;
                }
                int kills = 0;
                for (int t = 0; t < sizeT; t++)
                {
                    if (targetsHealth[t] == damage[t])
                        kills++;
                }
                killsArray.Add(kills);
            }
            // выбираем лучшие варианты
            int max = 0;
            foreach (var n in killsArray)
            {
                if (n > max)
                    max = n;
            }
            for (int i = 0; i < variants.Count;)
            {
                if (killsArray[i] < max)
                {
                    variants.RemoveAt(i);
                    killsArray.RemoveAt(i);
                }
                else
                {
                    i++;
                }
            }

            return variants;
        }
        void OptimizeOrderToRetreat()
        {
            // all retreats - ищем всех отступающих и строим глобальный граф отступления. Путь ищется до
            int startWeight = mapSize * mapSize;
            int CWfree = 1;
            int CWoptimizedUnit = -1;
            int CWdanger = -10;
            int CWenemy = -5;
            int CWmyBuilding = -2;
            int CWresources = -3;
            int CWtargetToRetreat = -7;
            int CWfreeAfterRetreat = -8;

            //init map of calculation
            int[][] map = new int[mapSize][];
            for (int x = 0; x < mapSize; x++)
                map[x] = new int[mapSize];

            List<XYWeight> findCells = new List<XYWeight>();
            List<int> tryRetreatEntitiesId = new List<int>();
            List<int> canRetreatEntitiesId = new List<int>();

            //init lists
            foreach (var em in entityMemories)
            {
                if (em.Value.order == EntityOrders.tryRetreat)
                {
                    tryRetreatEntitiesId.Add(em.Key);
                }
            }

            bool canFindNext = tryRetreatEntitiesId.Count > 0;

            // распутываем клубок
            while (canFindNext == true)
            {
                findCells.Clear();
                for (int x = 0; x < mapSize; x++)
                {
                    map[x] = new int[mapSize];
                }

                // fill findCells and map
                foreach (var tid in tryRetreatEntitiesId)
                {
                    findCells.Add(new XYWeight(entityMemories[tid].myEntity.Position.X, entityMemories[tid].myEntity.Position.Y, startWeight));
                    map[entityMemories[tid].myEntity.Position.X][entityMemories[tid].myEntity.Position.Y] = startWeight;
                }
                foreach (var cid in canRetreatEntitiesId)
                {
                    map[entityMemories[cid].myEntity.Position.X][entityMemories[cid].myEntity.Position.Y] = CWfreeAfterRetreat;
                    map[entityMemories[cid].movePos.X][entityMemories[cid].movePos.Y] = CWtargetToRetreat;
                }

                // ищем клетки через которые можно дойти до свободных клеток
                for (int kk = 0; kk < findCells.Count; kk++)
                {
                    int ex = findCells[kk].x;
                    int ey = findCells[kk].y;
                    int w = findCells[kk].weight;

                    if ((map[ex][ey] == CWfree || map[ex][ey] == CWfreeAfterRetreat) == false) // не продолжать поиск от свободных ячеек
                    {
                        for (int rlud = 0; rlud < 4; rlud++) // RightLeftUpDown
                        {
                            int nx = ex;
                            int ny = ey;
                            if (rlud == 0) nx++;
                            if (rlud == 1) nx--;
                            if (rlud == 2) ny++;
                            if (rlud == 3) ny--;

                            if (nx >= 0 && nx < mapSize && ny >= 0 && ny < mapSize)
                            {
                                if (map[nx][ny] == CWfreeAfterRetreat) // особая логика для освобождаемой клетки на предыдущем шаге
                                {
                                    findCells.Add(new XYWeight(nx, ny, w - 1)); // отмечаем, что туда можно прийти, вес уже есть
                                    findCells[kk] = new XYWeight(ex, ey, w, findCells[kk].index + 1); // считаем количество свободных клеток у сущности
                                    bool cellNotFinded = true;
                                    for (int nnn = 0; nnn < findCells.Count; nnn++)
                                    {
                                        if (findCells[nnn].x == nx && findCells[nnn].y == ny)
                                        {
                                            findCells[nnn] = new XYWeight(nx, ny, findCells[nnn].weight, findCells[nnn].index - 1); // считаем количество соседей у свободной клетки
                                            cellNotFinded = false;
                                            break;
                                        }
                                    }
                                    if (cellNotFinded == true)
                                        findCells.Add(new XYWeight(nx, ny, w - 1, -1)); // добавляем свободную клетку если она не была найдена
                                }
                                else
                                {
                                    if (map[nx][ny] == CWfree)
                                    {
                                        for (int nnn = 0; nnn < findCells.Count; nnn++)
                                        {
                                            if (findCells[nnn].x == nx && findCells[nnn].y == ny)
                                            {
                                                findCells[nnn] = new XYWeight(nx, ny, findCells[nnn].weight, findCells[nnn].index - 1); // считаем количество соседей у свободной клетки
                                                break;
                                            }
                                        }
                                        findCells[kk] = new XYWeight(ex, ey, w, findCells[kk].index + 1); // считаем количество свободных клеток
                                    }
                                    else
                                    {

                                        if (map[nx][ny] == 0)// поиск двигается через моих подданных, в которых он еще не был (в начале они не отступали)
                                        {
                                            // it is danger cell?
                                            EnemyDangerCell cell = enemyDangerCells[nx][ny];
                                            if (cell.meleesAim + cell.meleesWarning + cell.rangersAim + cell.rangersWarning + cell.turretsAim > 0) // it is danger!
                                            {
                                                map[nx][ny] = CWdanger;
                                            }
                                            else
                                            {
                                                int id = cellWithIdAny[nx][ny];
                                                if (id > 0) // здесь ктото есть
                                                {
                                                    if (enemiesById.ContainsKey(id)) // it is enemy!
                                                    {
                                                        map[nx][ny] = CWenemy;
                                                    }
                                                    else if (entityMemories.ContainsKey(id)) // it is my entity!
                                                    {
                                                        if (properties[entityMemories[id].myEntity.EntityType].CanMove == false) // it is a building
                                                        {
                                                            map[nx][ny] = CWmyBuilding;
                                                        }
                                                        else // it is a unit!
                                                        {
                                                            if (entityMemories[id].optimized == false)
                                                            {
                                                                map[nx][ny] = w - 1;
                                                                findCells.Add(new XYWeight(nx, ny, w - 1));
                                                            }
                                                            else
                                                            {
                                                                map[nx][ny] = CWoptimizedUnit;
                                                            }
                                                        }

                                                    }
                                                    else // it is resources!
                                                    {
                                                        map[nx][ny] = CWresources;
                                                    }
                                                }
                                                else
                                                {
                                                    map[nx][ny] = CWfree;// можем хранить здесь разные значения на карте и в массиве
                                                    findCells.Add(new XYWeight(nx, ny, w - 1, -1));
                                                    findCells[kk] = new XYWeight(ex, ey, w, findCells[kk].index + 1); // считаем количество свободных клеток
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (tryRetreatEntitiesId.Count + tryRetreatEntitiesId.Count > 1)
                {
                    ; // место остановки для тестов
                }


                // распутываем начиная со свободных клеток с одним соседом
                //int freeWeight = 0;
                bool isFindCellsWithOneFreeNeighpour = false;
                for (int num = 0; num < findCells.Count; num++)
                {
                    int fx = findCells[num].x;
                    int fy = findCells[num].y;
                    int fw = findCells[num].weight;
                    int fi = findCells[num].index;
                    int mw = map[fx][fy];

                    if ((mw == CWfree || mw == CWfreeAfterRetreat) == false)
                    {
                        if (fi == 1)
                        {
                            /// can be optimized 
                            /// Check All OneToOne, 
                            /// check 1 OneToSome
                            /// check 1 SometoOne/Some
                            isFindCellsWithOneFreeNeighpour = true;
                            int id = cellWithIdAny[fx][fy];
                            tryRetreatEntitiesId.Remove(id);
                            canRetreatEntitiesId.Add(id);

                            for (int rlud = 0; rlud < 4; rlud++) // RightLeftUpDown
                            {
                                int nx = fx;
                                int ny = fy;
                                if (rlud == 0) nx++;
                                if (rlud == 1) nx--;
                                if (rlud == 2) ny++;
                                if (rlud == 3) ny--;

                                if (nx >= 0 && nx < mapSize && ny >= 0 && ny < mapSize)
                                {
                                    if (map[nx][ny] == CWfree || map[nx][ny] == CWfreeAfterRetreat)
                                    {
                                        entityMemories[id].OrderCanRetreat(new Vec2Int(nx, ny), true, true, true);
                                        nextPositionMyUnitsMap[nx][ny] = id;
                                        break;
                                    }
                                }
                                //int g = 5;// как это не нашлась клетка?
                            }
                            break;
                        }
                    }
                }

                // иначе распутываем свободные клетки с несколькими соседями
                bool isFindCellsWithSomeFreeNeighpours = false;
                if (isFindCellsWithOneFreeNeighpour == false)
                {
                    for (int num = 0; num < findCells.Count; num++)
                    {
                        int fx = findCells[num].x;
                        int fy = findCells[num].y;
                        int fw = findCells[num].weight;
                        int fi = findCells[num].index;
                        int mw = map[fx][fy];

                        if ((mw == CWfree || mw == CWfreeAfterRetreat) == false)
                        {
                            if (fi > 1)
                            {
                                /// can be optimized 
                                /// Check All OneToOne, 
                                /// check 1 OneToSome
                                /// check 1 SometoOne/Some
                                isFindCellsWithSomeFreeNeighpours = true;
                                int id = cellWithIdAny[fx][fy];
                                tryRetreatEntitiesId.Remove(id);
                                canRetreatEntitiesId.Add(id);

                                for (int rlud = 0; rlud < 4; rlud++) // RightLeftUpDown
                                {
                                    int nx = fx;
                                    int ny = fy;
                                    if (rlud == 0) nx++;
                                    if (rlud == 1) nx--;
                                    if (rlud == 2) ny++;
                                    if (rlud == 3) ny--;

                                    if (nx >= 0 && nx < mapSize && ny >= 0 && ny < mapSize)
                                    {
                                        if (map[nx][ny] == CWfree || map[nx][ny] == CWfreeAfterRetreat)
                                        {
                                            entityMemories[id].OrderCanRetreat(new Vec2Int(nx, ny), true, true, true);
                                            nextPositionMyUnitsMap[nx][ny] = id;
                                            break;
                                        }
                                    }
                                    //int g = 5;// как это не нашлась клетка?
                                }
                                break;
                            }
                        }
                    }
                }

                // если нет свободных клеток, то отмечаем всех кто не смог сбежать на атаку и выходим из расчета
                if (isFindCellsWithOneFreeNeighpour == false
                    && isFindCellsWithSomeFreeNeighpours == false)
                {
                    canFindNext = false;
                    foreach (var id in tryRetreatEntitiesId)
                    {
                        int x = entityMemories[id].myEntity.Position.X;
                        int y = entityMemories[id].myEntity.Position.Y;
                        nextPositionMyUnitsMap[x][y] = id;
                        entityMemories[id].OrderAttack(null,
                            new AutoAttack(
                            properties[entityMemories[id].myEntity.EntityType].Attack.Value.AttackRange,
                            entityTypesArray),
                            false);
                    }
                }
            }


            //просмотре состояния
            if (debugOptions[(int)DebugOptions.canDrawGetAction] && debugOptions[(int)DebugOptions.drawRetreat])
            {
                DebugState debugState = _debugInterface.GetState();
                _debugInterface.Send(new DebugCommand.SetAutoFlush(false));

                if (_playerView.Players[0].Id == myId)
                {

                    bool drawMap = false;
                    int maxXY = mapSize;
                    #region draw map
                    if (drawMap == true && findCells.Count > 0)
                    {
                        int maxWeight = mapSize * mapSize;
                        for (int x = 0; x < maxXY; x++)
                        {
                            for (int y = 0; y < maxXY; y++)
                            {
                                int weight = map[x][y];
                                if (weight == CWfree)
                                {
                                    ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y + 0.3f), new Vec2Float(0, 0), colorGreen);
                                    _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, "F", 0.5f, 14)));
                                }
                                else if (weight == CWdanger)
                                {
                                    ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y + 0.3f), new Vec2Float(0, 0), colorRed);
                                    _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, "x", 0.5f, 14)));
                                }
                                else if (weight == CWenemy)
                                {
                                    ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y + 0.3f), new Vec2Float(0, 0), colorMagenta);
                                    _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, "!", 0.5f, 14)));
                                }
                                else if (weight == CWmyBuilding)
                                {
                                    ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y + 0.3f), new Vec2Float(0, 0), colorWhite);
                                    _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, "x", 0.5f, 14)));
                                }
                                else if (weight == CWresources)
                                {
                                    ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y + 0.3f), new Vec2Float(0, 0), colorWhite);
                                    _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, "x", 0.5f, 14)));
                                }
                                else if (weight == 0)
                                {
                                    //ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y + 0.3f), new Vec2Float(0, 0), colorMagenta);
                                    //_debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, "R", 0.5f, 14)));
                                }
                                else if (weight == startWeight)
                                {
                                    ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y + 0.3f), new Vec2Float(0, 0), colorBlack);
                                    _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, "R", 0.5f, 16)));
                                }
                                else if (weight < maxWeight)
                                {
                                    ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y + 0.3f), new Vec2Float(0, 0), colorMagenta);
                                    _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, (maxWeight - weight).ToString(), 0.5f, 16)));
                                }
                            }
                        }
                    }
                    #endregion

                    foreach (var id in tryRetreatEntitiesId)
                    {
                        ColoredVertex position = new ColoredVertex(new Vec2Float(
                            entityMemories[id].myEntity.Position.X + 0.5f,
                            entityMemories[id].myEntity.Position.Y + 0.3f), new Vec2Float(0, 0),
                            colorRed);
                        _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, "X", 0.5f, 16)));
                    }
                    foreach (var id in canRetreatEntitiesId)
                    {
                        ColoredVertex[] vertices = new ColoredVertex[] {
                            new ColoredVertex(new Vec2Float(entityMemories[id].myEntity.Position.X + 0.5f, entityMemories[id].myEntity.Position.Y + 0.5f), new Vec2Float(), colorBlack),
                            new ColoredVertex(new Vec2Float(entityMemories[id].movePos.X +0.5f, entityMemories[id].movePos.Y + 0.5f), new Vec2Float(), colorGreen),
                        };
                        DebugData.Primitives lines = new DebugData.Primitives(vertices, PrimitiveType.Lines);
                        _debugInterface.Send(new DebugCommand.Add(lines));
                    }
                }
                #region примеры использования
                //if (playerView.CurrentTick == 10)
                //{
                //    debugInterface.Send(new DebugCommand.Add(new DebugData.Log("Тестовое сообщение")));

                //    ColoredVertex position = new ColoredVertex(null, new Vec2Float(10, 10), colorGreen);
                //    DebugData.PlacedText text = new DebugData.PlacedText(position, "Ghbdtn", 0, 16);
                //    debugInterface.Send(new DebugCommand.Add(text));

                //    ColoredVertex[] vertices = new ColoredVertex[] {
                //        new ColoredVertex(new Vec2Float(7,7), new Vec2Float(), colorRed),
                //        new ColoredVertex(new Vec2Float(17,7), new Vec2Float(), colorRed),
                //        new ColoredVertex(new Vec2Float(20,20), new Vec2Float(), colorRed),
                //        new ColoredVertex(new Vec2Float(10,10), new Vec2Float(), colorRed)
                //    };
                //    DebugData.Primitives lines = new DebugData.Primitives(vertices, PrimitiveType.Lines);
                //    debugInterface.Send(new DebugCommand.Add(lines));
                //}
                #endregion
                //_debugInterface.Send(new DebugCommand.Flush());
            }

        }
        class CellWI
        {
            public int weight;
            public int index;
            public CellWI()
            {
                weight = 0;
                index = 0;
            }
        }
        void OptimizeOrderToRepairNew()
        {
            // проверяем по очереди приказы на ремонт нового здания            
            for (int ni = 0; ni < intentions.Count;)
            {
                bool deleteIntention = false;
                if (intentions[ni].intentionType == IntentionType.IntentionRepairNewBuilding)// для каждого делаем:
                {
                    deleteIntention = FindBuildersToBuildOrRepair(intentions[ni]);
                }
                // удаляем намерение или переходим к следующему
                if (deleteIntention)
                {
                    intentions.RemoveAt(ni);
                }
                else
                {
                    ni++;
                }
            }
        }
        void OptimizeOrderToBuildNew()
        {
            // проверяем по очереди приказы на строительство
            // для каждого делаем:
            for (int ni = 0; ni < intentions.Count;)
            {
                bool deleteIntention = false;
                if (intentions[ni].intentionType == IntentionType.IntentionCreateHouse
                    || intentions[ni].intentionType == IntentionType.IntentionCreateRangedBase
                    || intentions[ni].intentionType == IntentionType.IntentionCreateTurret)
                {
                    deleteIntention = FindBuildersToBuildOrRepair(intentions[ni]);
                }
                // удаляем намерение или переходим к следующему
                if (deleteIntention)
                {
                    intentions.RemoveAt(ni);
                }
                else
                {
                    ni++;
                }
            }
        }
        void OptimizeOrderToRepairOld()
        {
            // проверяем по очереди приказы на ремонт нового здания            
            for (int ni = 0; ni < intentions.Count;)
            {
                bool deleteIntention = false;
                if (intentions[ni].intentionType == IntentionType.IntentionRepairOldBuilding)// для каждого делаем:
                {
                    deleteIntention = FindBuildersToBuildOrRepair(intentions[ni]);
                }
                // удаляем намерение или переходим к следующему
                if (deleteIntention)
                {
                    intentions.RemoveAt(ni);
                }
                else
                {
                    ni++;
                }
            }
        }
        void OptAttackBlindRangers()
        {
            foreach(var en in entityMemories)
            {
                if (en.Value.optimized == false && en.Value.myEntity.EntityType == EntityType.RangedUnit)
                {
                    en.Value.OrderMoveAndAttack(
                        FindNearestEnemy(en.Value.myEntity.Position.X, en.Value.myEntity.Position.Y),
                        true,
                        true,
                        null,
                        new AutoAttack(properties[en.Value.myEntity.EntityType].SightRange, new EntityType[] { }),
                        true
                        );                        
                }
            }
        }

        bool FindBuildersToBuildOrRepair(Intention intention)
        {
            bool deleteIntention = false;

            int sx = 0;
            int sy = 0;
            int size = 1;
            int maxHealth = 1;
            int currentHealth = 0;

            switch (intention.intentionType) // различия
            {
                case IntentionType.IntentionCreateRangedBase:
                case IntentionType.IntentionCreateTurret:
                case IntentionType.IntentionCreateHouse:
                    sx = intention.targetPosition.X;
                    sy = intention.targetPosition.Y;
                    size = properties[intention.targetEntityType].Size;
                    maxHealth = properties[intention.targetEntityType].MaxHealth;
                    break;
                case IntentionType.IntentionRepairNewBuilding:
                case IntentionType.IntentionRepairOldBuilding:
                    sx = entityMemories[intention.targetId].myEntity.Position.X;
                    sy = entityMemories[intention.targetId].myEntity.Position.Y;
                    size = properties[entityMemories[intention.targetId].myEntity.EntityType].Size;
                    maxHealth = properties[entityMemories[intention.targetId].myEntity.EntityType].MaxHealth;
                    currentHealth = entityMemories[intention.targetId].myEntity.Health;
                    break;
                default: throw new System.Exception("Неизвестное намерение");
            }

            if (intention.intentionType == IntentionType.IntentionCreateHouse
                || intention.intentionType == IntentionType.IntentionCreateRangedBase
                || intention.intentionType == IntentionType.IntentionCreateTurret) // == различие - проверяем начинку только для стройки нового здания
            {
                for (int cx = sx; cx < sx + size; cx++) // проверка
                {
                    for (int cy = sy; cy < sy + size; cy++)
                    {
                        if (cellWithIdAny[cx][cy] >= 0)// проверяем что никого нет внутри сейчас
                        {
                            deleteIntention = true;
                            break;
                        }

                        if (nextPositionMyUnitsMap[cx][cy] > 0) // проверяем что никто не собирается идти внутрь стройки
                        {
                            deleteIntention = true;
                            break;
                        }

                    }
                    if (deleteIntention == true)// иначе отменяем намерение на строительство
                        break;
                }
            }

            if (deleteIntention == false) // помех нет, ищем строителей
            {
                #region стартовые значения
                CellWI[,] pathMap = new CellWI[mapSize, mapSize];
                for (int x = 0; x < mapSize; x++)
                {
                    for (int y = 0; y < mapSize; y++)
                    {
                        pathMap[x, y] = new CellWI();
                    }
                }

                //стартовое значение, которое будем уменьшать
                int startWeight = mapSize * mapSize;
                int minWeight = startWeight - maxHealth;
                int WInside = -1;
                int WBuilding = -2;
                int WEnemy = -3;
                int WResource = -4;
                int WDanger = -5;
                int WWarrior = -6;
                int WNextPosition = -7;
                int WDeniedBuilder = -8;
                int WUnvisibleCell = -9;
                #endregion

                #region определяем стартовые клетки
                List<int> borderMansId = new List<int>();
                //добавляем стартовые клетки поиска вокруг места строительства + юнитов на границе + отмечаем непроходимые клетки (здания, ресурсы, враги)
                List<XYWeight> findCells = new List<XYWeight>();
                for (int m = 0; m < size; m++)
                {
                    for (int h = 0; h < 4; h++)
                    {
                        int fx = sx;
                        int fy = sy;
                        if (h == 0)
                        {
                            fx = sx + m;
                            fy = sy + size;
                        }
                        else if (h == 1)
                        {
                            fx = sx + m;
                            fy = sy - 1;
                        }
                        else if (h == 2)
                        {
                            fx = sx - 1;
                            fy = sy + m;
                        }
                        else if (h == 3)
                        {
                            fx = sx + size;
                            fy = sy + m;
                        }
                        if (fx >= 0 && fx < mapSize && fy >= 0 && fy < mapSize)
                        {
                            int id = cellWithIdAny[fx][fy];
                            if (id >= 0)
                            {
                                if (entityMemories.ContainsKey(id))
                                {
                                    if (properties[entityMemories[id].myEntity.EntityType].CanMove)// только юниты, здания здесь не нужны
                                    {
                                        findCells.Add(new XYWeight(fx, fy, startWeight)); //  ищем от тех что стоит на границе
                                        pathMap[fx, fy].weight = startWeight;
                                        borderMansId.Add(id); // учитываем тех кто стоит на границе места строительства в том числе Могут быть войны
                                    }
                                    else
                                    {
                                        pathMap[fx, fy].weight = WBuilding;
                                    }
                                }
                                else if (enemiesById.ContainsKey(id))
                                {
                                    pathMap[fx, fy].weight = WEnemy;
                                }
                                else
                                {
                                    pathMap[fx, fy].weight = WResource;
                                }
                            }
                            else
                            {
                                findCells.Add(new XYWeight(fx, fy, startWeight));
                                pathMap[fx, fy].weight = startWeight;
                            }
                        }
                    }
                }

                // обозначаем клетки внутри здания
                for (int cx = sx; cx < sx + size; cx++)
                {
                    for (int cy = sy; cy < sy + size; cy++)
                    {
                        pathMap[cx, cy].weight = WInside;
                    }
                }
                #endregion

                #region опредеялем количество соседов строителей и сколько нам осталось искать
                int builderCount = 0;
                foreach (var id in borderMansId)
                {
                    if (entityMemories[id].myEntity.EntityType == EntityType.BuilderUnit)
                    {
                        if (entityMemories[id].optimized == false)
                        {
                            builderCount++;
                            if (debugOptions[(int)DebugOptions.drawBuildAndRepairOrder])
                            {
                                DrawLineOnce(entityMemories[id].myEntity.Position.X + 0.5f, entityMemories[id].myEntity.Position.Y + 0.5f, sx + size / 2f, sy + size / 2f, colorGreen, colorGreen);
                            }
                            switch (intention.intentionType) // == diference
                            {
                                case IntentionType.IntentionCreateRangedBase:
                                case IntentionType.IntentionCreateTurret:
                                case IntentionType.IntentionCreateHouse:
                                    entityMemories[id].OrderBuild(
                                        new Vec2Int(sx, sy),
                                        intention.targetEntityType,
                                        new Vec2Int(entityMemories[id].myEntity.Position.X, entityMemories[id].myEntity.Position.Y),
                                        false,
                                        false,
                                        true);
                                    break;
                                case IntentionType.IntentionRepairNewBuilding:
                                case IntentionType.IntentionRepairOldBuilding:
                                    entityMemories[id].OrderRepairGo(
                                        intention.targetId,
                                        new Vec2Int(entityMemories[id].myEntity.Position.X, entityMemories[id].myEntity.Position.Y),
                                        false,
                                        false,
                                        true
                                        );
                                    break;
                                default: throw new System.Exception("Неизвестное намерение");
                            }
                        }
                    }
                }

                int planDistance = 0;
                int planHealth = currentHealth;
                int prevDistContact = 0;
                if (builderCount > 0)
                {//ищем максимум на половину оставшегося времени строительства
                    planDistance = (maxHealth - planHealth) / builderCount;
                    int remains = 0;
                    switch (intention.intentionType)// == difference
                    {
                        case IntentionType.IntentionCreateRangedBase:
                        case IntentionType.IntentionCreateTurret:
                        case IntentionType.IntentionCreateHouse:
                            remains = (int)((float)planDistance * buildBuildingDistCoef);
                            break;
                        case IntentionType.IntentionRepairNewBuilding:
                        case IntentionType.IntentionRepairOldBuilding:
                            remains = (int)((float)planDistance * repairBuildingDistCoef);
                            break;
                        default: throw new System.Exception("Неизвестное намерение");
                    }
                    minWeight = startWeight - remains;
                }
                #endregion

                #region стартовые группы и место в них
                // объединяем стартовые ячейки в группы, у каждой группы соседей теперь должен быть одинаковый индекс (порядок номеров не важен, могут быть пропуски)
                int lastIndex = 1; // 0 используется у пустых групп
                for (int iter = 0; iter < findCells.Count; iter++)
                {
                    int myIndex = findCells[iter].index;
                    if (myIndex == 0)
                    {
                        myIndex = lastIndex;
                        findCells[iter].index = myIndex;
                        lastIndex++;
                    }

                    // ищем всех соседей и проверяем их индекс
                    int mx = findCells[iter].x;
                    int my = findCells[iter].y;
                    for (int i = iter + 1; i < findCells.Count; i++)
                    {
                        int dist = Abs(mx - findCells[i].x) + Abs(my - findCells[i].y);

                        if (dist == 1)// это мой сосед
                        {
                            if (findCells[i].index == 0)
                            {
                                findCells[i].index = myIndex;
                            }
                            else
                            {
                                // это старший брат, надо взять его индекс себе и всем кому уже присвоили мой индекс
                                int newIndex = findCells[i].index;
                                for (int n = 0; n < findCells.Count; n++)
                                {
                                    if (findCells[n].index == myIndex)
                                    {
                                        findCells[n].index = newIndex;
                                    }
                                }
                                myIndex = newIndex;
                            }
                        }
                    }
                }

                // учитываем количество свободных мест в группах
                Dictionary<int, int> freePlaceInIndexGroup = new Dictionary<int, int>();
                foreach (var c in findCells)
                {
                    pathMap[c.x, c.y].index = c.index;
                    if (freePlaceInIndexGroup.ContainsKey(c.index))
                    {
                        freePlaceInIndexGroup[c.index]++;
                    }
                    else
                    {
                        freePlaceInIndexGroup[c.index] = 1;
                    }
                }
                foreach (var id in borderMansId) // убираем тех кто стоит на границе
                {
                    int x = entityMemories[id].myEntity.Position.X;
                    int y = entityMemories[id].myEntity.Position.Y;
                    for (int i = 0; i < findCells.Count; i++)
                    {
                        if (findCells[i].x == x && findCells[i].y == y)
                        {
                            freePlaceInIndexGroup[findCells[i].index]--;
                            findCells.RemoveAt(i);
                            break;
                        }
                    }
                }
                #endregion

                // начинаем искать свободных строителей
                for (int iter = 0; iter < findCells.Count; iter++)
                {
                    int fx = findCells[iter].x;
                    int fy = findCells[iter].y;
                    int fw = findCells[iter].weight;
                    int fi = findCells[iter].index;

                    if (fw > minWeight)
                    {

                        for (int jj = 0; jj < 4; jj++)
                        {
                            int nx = fx;
                            int ny = fy;
                            if (jj == 0) nx--;
                            if (jj == 1) ny--;
                            if (jj == 2) nx++;
                            if (jj == 3) ny++;

                            if (nx >= 0 && nx < mapSize && ny >= 0 && ny < mapSize) // все в границах карты
                            {
                                if (pathMap[nx, ny].weight == 0)
                                {
                                    bool canContinue = true;

                                    var dCell = enemyDangerCells[nx][ny];
                                    if (dCell.meleesAim + dCell.rangersAim + dCell.turretsAim > 0) // проверка опасной зоны
                                    {
                                        canContinue = false;
                                        pathMap[nx, ny].weight = WDanger;
                                    }
                                    else if (dCell.meleesWarning + dCell.rangersWarning > 0)
                                    {
                                        canContinue = false;
                                        pathMap[nx, ny].weight = WDanger;
                                    }
                                    else if (nextPositionMyUnitsMap[nx][ny] > 0) // проверка пустой позиции на следующий ход
                                    {
                                        canContinue = false;
                                        pathMap[nx, ny].weight = WNextPosition;
                                    }
                                    else if (onceVisibleMap[nx][ny] == 0)
                                    {
                                        canContinue = false;
                                        pathMap[nx, ny].weight = WUnvisibleCell;
                                    }

                                    if (canContinue == true)
                                    {
                                        int id = cellWithIdAny[nx][ny];
                                        if (id >= 0)// occupied cell
                                        {
                                            if (entityMemories.ContainsKey(id))
                                            {
                                                if (entityMemories[id].myEntity.EntityType == EntityType.BuilderUnit)
                                                {
                                                    // нашли строителя!!!
                                                    // при нахождении человека проверяем надо ли искать дальше
                                                    // при окончании места в группах обновляем

                                                    //if (w == startWeight)//check my builder на соседней клетке с ресурсомs
                                                    //{
                                                    //    //canContinueField = false;
                                                    //    //resourcePotentialField[nx][ny] = RPFdeniedBuilderWeight;
                                                    //}
                                                    if (entityMemories[id].optimized == false)
                                                    {
                                                        canContinue = false;
                                                        if (debugOptions[(int)DebugOptions.drawBuildAndRepairOrder])
                                                        {
                                                            DrawLineOnce(nx + 0.5f, ny + 0.5f, sx + size / 2f, sy + size / 2f, colorGreen, colorGreen);
                                                        }
                                                        if (debugOptions[(int)DebugOptions.drawBuildAndRepairPath])
                                                        {
                                                            DrawLineOnce(nx + 0.5f, ny + 0.5f, fx + 0.5f, fy + 0.5f, colorMagenta, colorMagenta);
                                                        }
                                                        pathMap[nx, ny].weight = WDeniedBuilder;
                                                        nextPositionMyUnitsMap[fx][fy] = id;
                                                        freePlaceInIndexGroup[pathMap[fx, fy].index]--;
                                                        // обновить минимальную дистанцию
                                                        int curDistance = startWeight - fw;
                                                        planHealth += (curDistance - prevDistContact) * builderCount;
                                                        builderCount++;
                                                        prevDistContact = curDistance;
                                                        planDistance = curDistance + (maxHealth - planHealth) / builderCount;
                                                        int remains = 0;
                                                        switch (intention.intentionType)// == difference
                                                        {
                                                            case IntentionType.IntentionCreateRangedBase:
                                                            case IntentionType.IntentionCreateTurret:
                                                            case IntentionType.IntentionCreateHouse:
                                                                remains = (int)((float)planDistance * buildBuildingDistCoef);
                                                                entityMemories[id].OrderGoToBuild(new Vec2Int(fx, fy), true, true, true);
                                                                break;
                                                            case IntentionType.IntentionRepairNewBuilding:
                                                            case IntentionType.IntentionRepairOldBuilding:
                                                                remains = (int)((float)planDistance * repairBuildingDistCoef);
                                                                entityMemories[id].OrderRepairGo(intention.targetId, new Vec2Int(fx, fy), true, true, true);
                                                                break;
                                                            default: throw new System.Exception("Неизвестное намерение");
                                                        }
                                                        minWeight = startWeight - remains;
                                                        // чистка если закончились места
                                                        if (freePlaceInIndexGroup[pathMap[fx, fy].index] == 0)
                                                        {
                                                            // обновить pathMap, убрать следы которые меньше startWeight, но больше 0
                                                            foreach (var c in findCells)
                                                            {
                                                                if (pathMap[c.x, c.y].weight > 0)
                                                                {
                                                                    if (pathMap[c.x, c.y].weight < startWeight)
                                                                        pathMap[c.x, c.y].weight = 0;
                                                                    pathMap[c.x, c.y].index = 0;
                                                                }
                                                            }
                                                            // новый список findcells без указанной группы
                                                            for (int i = 0; i < findCells.Count;)
                                                            {
                                                                bool del = false;
                                                                if (findCells[i].weight != startWeight)
                                                                    del = true;
                                                                else if (freePlaceInIndexGroup[findCells[i].index] == 0)
                                                                    del = true;

                                                                if (del)
                                                                    findCells.RemoveAt(i);
                                                                else
                                                                    i++;
                                                            }
                                                            // итераторы в самое начало
                                                            iter = 0;
                                                            //обновляем количество стартовых мест
                                                            freePlaceInIndexGroup.Clear();
                                                            foreach (var c in findCells)
                                                            {
                                                                pathMap[c.x, c.y].index = c.index;
                                                                if (freePlaceInIndexGroup.ContainsKey(c.index))
                                                                {
                                                                    freePlaceInIndexGroup[c.index]++;
                                                                }
                                                                else
                                                                {
                                                                    freePlaceInIndexGroup[c.index] = 1;
                                                                }
                                                            }
                                                        }
                                                        break; // прекратить поиск из этой клетки
                                                    }
                                                    else
                                                    {
                                                        canContinue = false;
                                                        pathMap[nx, ny].weight = WDeniedBuilder;
                                                    }

                                                }
                                                else
                                                {
                                                    if (properties[entityMemories[id].myEntity.EntityType].CanMove == false)//is my building
                                                    {
                                                        canContinue = false;
                                                        pathMap[nx, ny].weight = WBuilding;
                                                    }
                                                    else
                                                    {
                                                        canContinue = false;
                                                        pathMap[nx, ny].weight = WWarrior;
                                                    }
                                                }
                                            }
                                            else if (enemiesById.ContainsKey(id))// enemy 
                                            {
                                                canContinue = false;
                                                pathMap[nx, ny].weight = WDanger;
                                            }
                                            else // it is resource
                                            {
                                                canContinue = false;
                                                pathMap[nx, ny].weight = WResource;
                                            }
                                        }
                                    }

                                    if (canContinue == true) // empty, safe cell or through free unit
                                    {
                                        //add weight and findCell
                                        pathMap[nx, ny].weight = fw - 1;
                                        pathMap[nx, ny].index = fi;
                                        if (fw > minWeight)
                                        {
                                            findCells.Add(new XYWeight(nx, ny, fw - 1, fi));

                                            if (debugOptions[(int)DebugOptions.drawBuildAndRepairPath])
                                            {
                                                DrawLineOnce(nx + 0.5f, ny + 0.5f, fx + 0.5f, fy + 0.5f, colorMagenta, colorMagenta);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    // MOD можно не проверять на совпадения если осталась всего одна группа
                                    if (pathMap[nx, ny].weight > 0 && pathMap[nx, ny].weight < startWeight) // это уже пройденная клетка, надо проверить, будем ли объединяться
                                    {
                                        if (pathMap[nx, ny].index == 0)
                                        {
                                            ;// такого не должно быть что вес больше нуля, а индекса группы нет!!!
                                        }

                                        if (pathMap[nx, ny].index != fi)
                                        {// встретили клетку с другим индексом, значит надо объединить группы
                                            int oldIndex = pathMap[nx, ny].index;
                                            int newIndex = fi;

                                            if (freePlaceInIndexGroup.ContainsKey(oldIndex) == false || freePlaceInIndexGroup.ContainsKey(newIndex) == false)
                                            {
                                                ;
                                            }

                                            for (int x = 0; x < mapSize; x++)//обновляем карту
                                            {
                                                for (int y = 0; y < mapSize; y++)
                                                {
                                                    if (pathMap[x, y].index == oldIndex)
                                                        pathMap[x, y].index = newIndex;
                                                }
                                            }
                                            foreach (var c in findCells)//обновляем клетки поиска
                                            {
                                                if (c.index == oldIndex)
                                                    c.index = newIndex;
                                            }


                                            freePlaceInIndexGroup[newIndex] += freePlaceInIndexGroup[oldIndex];
                                            freePlaceInIndexGroup.Remove(oldIndex);
                                        }
                                    }
                                }
                                //можем не проверять уже занятые клетки, так как у нас волны распространяются по очереди 1-2-3-4 и т.д.
                            }
                        }
                        if (debugOptions[(int)DebugOptions.canDrawGetAction] && debugOptions[(int)DebugOptions.drawBuildAndRepairPath])
                        {
                            _debugInterface.Send(new DebugCommand.Flush());
                        }
                    }
                }
            }
            return deleteIntention;
        }
        void ConvertOrdersToActions()
        {
            // zeroing
            for (int x = 0; x < mapSize; x++)
            {
                nextPositionMyUnitsMap[x] = new int[mapSize];
            }

            actions.Clear();

            foreach (var em in entityMemories)
            {
                BuildAction buildAction = new BuildAction();
                MoveAction moveAction = new MoveAction();
                RepairAction repairAction = new RepairAction();
                AttackAction attackAction = new AttackAction();

                switch (em.Value.order)
                {
                    case EntityOrders.spawnUnit:
                        buildAction.EntityType = em.Value.targetEntityType;
                        buildAction.Position = em.Value.targetPos;
                        actions.Add(em.Key, new EntityAction(null, buildAction, null, null));
                        break;
                    case EntityOrders.cancelAll:
                        actions.Add(em.Key, new EntityAction(null, null, null, null));
                        break;
                    case EntityOrders.buildNow:
                        moveAction.BreakThrough = em.Value.moveBreakThrough;
                        moveAction.FindClosestPosition = em.Value.moveFindClosestPosition;
                        moveAction.Target = em.Value.movePos;

                        buildAction.EntityType = em.Value.targetEntityType;
                        buildAction.Position = em.Value.targetPos;

                        actions.Add(em.Key, new EntityAction(moveAction, buildAction, null, null));
                        break;
                    case EntityOrders.repairGo:
                        moveAction.BreakThrough = em.Value.moveBreakThrough;
                        moveAction.FindClosestPosition = em.Value.moveFindClosestPosition;
                        moveAction.Target = em.Value.movePos;

                        if (em.Value.targetId == null)
                            throw new System.Exception("Пытаюсь чинить не получив Id!");
                        repairAction.Target = em.Value.targetId ?? 0;
                        actions.Add(em.Key, new EntityAction(moveAction, null, null, repairAction));
                        break;
                    case EntityOrders.buildGo:
                    case EntityOrders.collect:
                    case EntityOrders.tryRetreat:
                    case EntityOrders.canRetreat:
                    case EntityOrders.pushed:
                    case EntityOrders.move:
                        moveAction.BreakThrough = em.Value.moveBreakThrough;
                        moveAction.FindClosestPosition = em.Value.moveFindClosestPosition;
                        moveAction.Target = em.Value.movePos;
                        actions.Add(em.Key, new EntityAction(moveAction, null, null, null));
                        break;
                    case EntityOrders.attack:
                        attackAction.Target = em.Value.targetId;
                        attackAction.AutoAttack = em.Value.autoAttack;
                        actions.Add(em.Key, new EntityAction(null, null, attackAction, null));
                        break;
                    case EntityOrders.moveAndAttack:
                        moveAction.BreakThrough = em.Value.moveBreakThrough;
                        moveAction.FindClosestPosition = em.Value.moveFindClosestPosition;
                        moveAction.Target = em.Value.movePos;

                        attackAction.AutoAttack = em.Value.autoAttack;
                        actions.Add(em.Key, new EntityAction(moveAction, null, attackAction, null));
                        break;
                    case EntityOrders.none:

                        break;
                    default:
                        throw new System.Exception("неизвестный приказ"); // тревога незнакомый приказ
                }
            }
        }

        void OrderCreateUnit(int baseId, bool agressive)
        {
            Vec2Int target;
            if (agressive)
            {
                target = FindSpawnPosition(entityMemories[baseId].myEntity.Position.X, entityMemories[baseId].myEntity.Position.Y, agressive);
            }
            else
            {
                //make builder
                target = FindNearestToBaseResourceReturnSpawnPlace(entityMemories[baseId].myEntity.Position.X, entityMemories[baseId].myEntity.Position.Y);
            }

            entityMemories[baseId].order = EntityOrders.spawnUnit;
            entityMemories[baseId].targetPos = target;
            entityMemories[baseId].targetEntityType = properties[entityMemories[baseId].myEntity.EntityType].Build.Value.Options[0];


            //BuildAction buildAction = new BuildAction();
            //Vec2Int target;
            //if (agressive)
            //{
            //    target = FindSpawnPosition(entityMemories[baseId].myEntity.Position.X, entityMemories[baseId].myEntity.Position.Y, agressive);
            //} else
            //{
            //    //make builder
            //    target = FindNearestToBaseResourceReturnSpawnPlace(entityMemories[baseId].myEntity.Position.X, entityMemories[baseId].myEntity.Position.Y);
            //    //target = new Vec2Int(a.startX, a.startY);
            //}

            //buildAction.EntityType = properties[entityMemories[baseId].myEntity.EntityType].Build.Value.Options[0];
            //buildAction.Position = target;

            //actions.Add(baseId, new EntityAction(null, buildAction, null, null));
        }
        void OrderCancelAll(int id)
        {
            entityMemories[id].order = EntityOrders.cancelAll;
        }
        void OrderCollectResources(int id)
        {
            int ex = entityMemories[id].myEntity.Position.X;
            int ey = entityMemories[id].myEntity.Position.Y;

            int tx = ex;
            int ty = ey;
            int maxW = 0;

            for (int i = 0; i < 4; i++)
            {
                int nx = ex;
                int ny = ey;
                if (i == 0) nx++;
                if (i == 1) nx--;
                if (i == 2) ny++;
                if (i == 3) ny--;

                if (nx >= 0 && nx < mapSize && ny >= 0 && ny < mapSize)
                {
                    if (resourcePotentialField[nx][ny] > maxW)
                    {
                        tx = nx;
                        ty = ny;
                        maxW = resourcePotentialField[nx][ny];
                    }
                }
            }

            entityMemories[id].order = EntityOrders.collect;
            //entityMemories[id].targetPos = new Vec2Int(tx, ty);// duplicate
            entityMemories[id].movePos = new Vec2Int(tx, ty);
            entityMemories[id].moveBreakThrough = true;
            entityMemories[id].moveFindClosestPosition = false;

            //MoveAction moveAction = new MoveAction();
            //moveAction.BreakThrough = true;
            //moveAction.FindClosestPosition = false;
            //moveAction.Target = new Vec2Int(tx, ty);

            ////AttackAction attackAction = new AttackAction();
            ////attackAction.AutoAttack = new AutoAttack(distance, new EntityType[] { EntityType.Resource });

            //actions.Add(id, new EntityAction(moveAction, null, null, null));
        }
        
        void OrderRetreatBuilderFromEnemy(int id)
        {
            int ex = entityMemories[id].myEntity.Position.X;
            int ey = entityMemories[id].myEntity.Position.Y;

            int tx = ex;
            int ty = ey;
            int maxFindWeight = 0;
            int limitWeight = mapSize * mapSize; // вес клеток ресурсов не подходит, так как надо убегать, а не добывать

            for (int i = 0; i < 4; i++)
            {
                int nx = ex;
                int ny = ey;
                if (i == 0) nx++;
                if (i == 1) nx--;
                if (i == 2) ny++;
                if (i == 3) ny--;

                if (nx >= 0 && nx < mapSize && ny >= 0 && ny < mapSize)
                {
                    int w = resourcePotentialField[nx][ny];
                    if ((w < limitWeight && w > maxFindWeight) || w == RPFdeniedBuilderWeight)//свободная клетка или клетка занятая строителем
                    {
                        tx = nx;
                        ty = ny;
                        maxFindWeight = w;
                    }
                }
            }

            if (maxFindWeight > 0) // есть путь отхода по клеткам RPF
            {
                entityMemories[id].order = EntityOrders.tryRetreat;
                entityMemories[id].moveBreakThrough = true;
                entityMemories[id].moveFindClosestPosition = false;
                entityMemories[id].movePos = new Vec2Int(tx, ty);
                //nextPositionMyUnitsMap[tx][ty] = id;

                //MoveAction moveAction = new MoveAction();
                //moveAction.BreakThrough = true;
                //moveAction.FindClosestPosition = false;
                //moveAction.Target = new Vec2Int(tx, ty);

                //actions.Add(id, new EntityAction(moveAction, null, null, null));
            }
            else
            {
                List<Vec2Int> targetsWarning = new List<Vec2Int>();
                List<Vec2Int> targetsSafe = new List<Vec2Int>();

                for (int k = 0; k < 4; k++)
                {
                    int x = ex;
                    int y = ey;
                    if (k == 0) x--;
                    if (k == 1) x++;
                    if (k == 2) y--;
                    if (k == 3) y++;

                    if (x >= 0 && y >= 0 && x < mapSize && y < mapSize) // valid XY
                    {
                        if (cellWithIdAny[x][y] < 0) // empty cell
                        {
                            var cell = enemyDangerCells[x][y];
                            if (cell.meleesAim + cell.meleesWarning + cell.rangersAim + cell.rangersWarning == 0)
                            {
                                targetsSafe.Add(new Vec2Int(x, y));
                            }
                            else
                            {
                                if (cell.meleesAim + cell.rangersAim == 0)
                                {
                                    targetsWarning.Add(new Vec2Int(x, y));
                                }
                            }
                        }
                    }
                }

                if (targetsSafe.Count > 0)
                {
                    entityMemories[id].order = EntityOrders.tryRetreat;
                    entityMemories[id].moveBreakThrough = false;
                    entityMemories[id].moveFindClosestPosition = true;
                    Vec2Int target = targetsSafe[random.Next(targetsSafe.Count)];
                    entityMemories[id].movePos = target;
                    //nextPositionMyUnitsMap[target.X][target.Y] = id;

                    //MoveAction moveAction = new MoveAction();
                    //moveAction.BreakThrough = false;
                    //moveAction.FindClosestPosition = true;
                    //moveAction.Target = targetsSafe[random.Next(targetsSafe.Count)];
                    //debugLines.Add(new DebugLine(ex + 0.5f, ey + 0.5f, moveAction.Target.X + 0.5f, moveAction.Target.Y + 0.5f, colorGreen, colorGreen));
                    //actions.Add(id, new EntityAction(moveAction, null, null, null));

                }
                else if (targetsWarning.Count > 0)
                {
                    entityMemories[id].order = EntityOrders.tryRetreat;
                    entityMemories[id].moveBreakThrough = false;
                    entityMemories[id].moveFindClosestPosition = true;
                    Vec2Int target = targetsWarning[random.Next(targetsWarning.Count)];
                    entityMemories[id].movePos = target;
                    //nextPositionMyUnitsMap[target.X][target.Y] = id;

                    //MoveAction moveAction = new MoveAction();
                    //moveAction.BreakThrough = false;
                    //moveAction.FindClosestPosition = true;
                    //moveAction.Target = targetsWarning[random.Next(targetsWarning.Count)];
                    //debugLines.Add(new DebugLine(ex + 0.5f, ey + 0.5f, moveAction.Target.X + 0.5f, moveAction.Target.Y + 0.5f, colorBlue, colorBlue));
                    //actions.Add(id, new EntityAction(moveAction, null, null, null));

                }
                else
                {
                    entityMemories[id].order = EntityOrders.attack;
                    entityMemories[id].autoAttack = new AutoAttack(properties[entityMemories[id].myEntity.EntityType].SightRange, entityTypesArray); // атаковать абсолютно всех

                    //AttackAction attackAction = new AttackAction();
                    //attackAction.AutoAttack = new AutoAttack(properties[entityMemories[id].myEntity.EntityType].SightRange, entityTypesArray); // атаковать абсолютно всех
                    //debugLines.Add(new DebugLine(ex, ey, ex + 1, ey + 1, colorRed, colorRed));
                    //actions.Add(id, new EntityAction(null, null, attackAction, null));
                }
            }
        }
        void OrderTurretAttack(int id)
        {
            int range = properties[EntityType.Turret].SightRange;

            bool[] availableTargetsType = FindAvailableTargetType(
                entityMemories[id].myEntity.Position.X,
                entityMemories[id].myEntity.Position.Y,
                properties[EntityType.Turret].Size,
                range);

            entityMemories[id].order = EntityOrders.attack;

            //AttackAction attackAction = new AttackAction();
            if (availableTargetsType[(int)EntityType.RangedUnit] == true)
                entityMemories[id].autoAttack = new AutoAttack(range, new EntityType[] { EntityType.RangedUnit });
            else if (availableTargetsType[(int)EntityType.MeleeUnit] == true)
                entityMemories[id].autoAttack = new AutoAttack(range, new EntityType[] { EntityType.RangedUnit, EntityType.MeleeUnit });
            else if (availableTargetsType[(int)EntityType.BuilderUnit] == true)
                entityMemories[id].autoAttack = new AutoAttack(range, new EntityType[] { EntityType.RangedUnit, EntityType.MeleeUnit, EntityType.BuilderUnit });
            else
                entityMemories[id].autoAttack = new AutoAttack(range, new EntityType[] { });

            //actions.Add(id, new EntityAction(null, null, attackAction, null));
        }
        void OrderAttackNearbyEnemy(int id, EntityType[] entityTypes)
        {

            entityMemories[id].order = EntityOrders.moveAndAttack;
            entityMemories[id].moveBreakThrough = true;
            entityMemories[id].moveFindClosestPosition = true;
            entityMemories[id].movePos = FindNearestEnemy(entityMemories[id].myEntity.Position.X, entityMemories[id].myEntity.Position.Y);

            entityMemories[id].autoAttack = new AutoAttack(properties[entityMemories[id].myEntity.EntityType].SightRange, entityTypes);

            //MoveAction moveAction = new MoveAction();
            //moveAction.BreakThrough = true;
            //moveAction.FindClosestPosition = true;
            //moveAction.Target = FindNearestEnemy(entityMemories[id].myEntity.Position.X, entityMemories[id].myEntity.Position.Y);

            //AttackAction attackAction = new AttackAction();
            //attackAction.AutoAttack = new AutoAttack(properties[entityMemories[id].myEntity.EntityType].SightRange, entityTypes);

            //actions.Add(id, new EntityAction(moveAction, null, attackAction, null));
        }
               
        bool[] FindAvailableTargetType(int sx, int sy, int size, int range)
        {
            bool[] availableType = new bool[entityTypesArray.Length];
            foreach (var i in entityTypesArray)
            {
                availableType[(int)i] = false;
            }

            EntityType type = EntityType.Resource;

            int sxRight = sx + size - 1;
            int syUp = sy + size - 1;
            for (int si = 0; si < size; si++)
            {
                //my base
                for (int siy = 0; siy < size; siy++)
                {
                    if (GetEnemiesTypeSafeByXY(sx + si, sy + siy, ref type)) availableType[(int)type] = true;
                }

                //straight
                for (int di = 1; di < range; di++)
                {
                    if (GetEnemiesTypeSafeByXY(sx - di, sy + si, ref type)) availableType[(int)type] = true; // left
                    if (GetEnemiesTypeSafeByXY(sx + si, sy - di, ref type)) availableType[(int)type] = true; // down
                    if (GetEnemiesTypeSafeByXY(sxRight + di, sy + si, ref type)) availableType[(int)type] = true; // right
                    if (GetEnemiesTypeSafeByXY(sx + si, syUp + di, ref type)) availableType[(int)type] = true; // up
                }
            }
            //diagonal
            for (int aa = 1; aa < range - 1; aa++)
            {
                for (int bb = 1; bb < range - aa; bb++)
                {
                    if (GetEnemiesTypeSafeByXY(sx - aa, sy - bb, ref type)) availableType[(int)type] = true; // left-down
                    if (GetEnemiesTypeSafeByXY(sx - aa, syUp + bb, ref type)) availableType[(int)type] = true; // left-up
                    if (GetEnemiesTypeSafeByXY(sxRight + aa, syUp + bb, ref type)) availableType[(int)type] = true; // right-up
                    if (GetEnemiesTypeSafeByXY(sxRight + aa, sy - bb, ref type)) availableType[(int)type] = true; // right-down
                }
            }
            return availableType;
        }
        bool GetEnemiesTypeSafeByXY(int x, int y, ref EntityType type)
        {
            if (x >= 0 && x < mapSize && y >= 0 && y < mapSize)
            {
                if (cellWithIdAny[x][y] >= 0)
                {
                    return GetEnemiesTypeSafe(cellWithIdAny[x][y], ref type);
                }
            }
            return false;
        }
        bool GetEnemiesTypeSafe(int enemyID, ref EntityType type)
        {
            if (enemiesById.ContainsKey(enemyID))
            {
                type = enemiesById[enemyID].EntityType;
                return true;
            }
            else
            {
                return false;
            }
        }

        int Distance(int x1, int y1, int size1, int x2, int y2, int size2)
        {
            if (size1 == 1 && size2 == 1)
            {
                return Abs(x1 - x2) + Abs(y1 - y2);
            }
            else
            {
                int x1right = x1 + size1 - 1;
                int y1up = y1 + size1 - 1;
                int x2right = x2 + size2 - 1;
                int y2up = y2 + size2 - 1;

                int dx = 0;
                if (x1right < x2) dx = Abs(x1right - x2);
                else if (x2right < x1) dx = Abs(x2right - x1);

                int dy = 0;
                if (y1up < y2) dy = Abs(y1up - y2);
                else if (y2up < y1) dy = Abs(y2up - y1);

                return dx + dy;
            }
        }

        Vec2Int FindPositionFromOurCorner(EntityType entityType)
        {
            int buildingSize = properties[entityType].Size;

            int[,] pathMap = new int[mapSize, mapSize];
            //стартовое значение, которое будем уменьшать
            int startWeight = 15;
            int minWeight = 0;
            int WBusy = -1;
            //int WBuilding = -2;
            int WEnemy = -3;
            int WResource = -4;
            int WDanger = -5;
            //int WWarrior = -6;


            #region определяем стартовые клетки
            //добавляем стартовые клетки поиска
            List<XYWeight> findCells = new List<XYWeight>();
            foreach (var en in entityMemories)
            {
                if (en.Value.myEntity.EntityType == EntityType.BuilderUnit)
                {
                    switch (en.Value.prevOrder)
                    {
                        case EntityOrders.attack:
                        case EntityOrders.moveAndAttack:
                        case EntityOrders.buildNow:
                        case EntityOrders.repairGo:
                        case EntityOrders.tryRetreat:
                        case EntityOrders.canRetreat:
                        case EntityOrders.pushed:
                            break;
                        case EntityOrders.cancelAll:
                        case EntityOrders.collect:
                        case EntityOrders.buildGo:
                        case EntityOrders.move:
                        case EntityOrders.none:
                            pathMap[en.Value.myEntity.Position.X, en.Value.myEntity.Position.Y] = startWeight;
                            findCells.Add(new XYWeight(en.Value.myEntity.Position.X, en.Value.myEntity.Position.Y, startWeight));
                            break;
                        default:
                            throw new System.Exception("Неизвестный старый приказ: " + nameof(en.Value.prevOrder) + " - " + en.Value.prevOrder);
                    }
                }
            }
            #endregion

            #region генерируем карту близости к строителям
            while (findCells.Count > 0)
            {
                int bx = findCells[0].x;
                int by = findCells[0].y;
                int w = findCells[0].weight;
                if (w > minWeight)
                {

                    for (int jj = 0; jj < 4; jj++)
                    {
                        int nx = bx;
                        int ny = by;
                        if (jj == 0) nx--;
                        if (jj == 1) ny--;
                        if (jj == 2) nx++;
                        if (jj == 3) ny++;

                        if (nx >= 0 && nx < mapSize && ny >= 0 && ny < mapSize)
                        {
                            if (pathMap[nx, ny] == 0)
                            {
                                bool canContinueField = true;

                                // проверка опасной зоны
                                var dCell = enemyDangerCells[nx][ny];
                                if (dCell.meleesAim + dCell.rangersAim + dCell.turretsAim > 0)
                                {
                                    canContinueField = false;
                                    pathMap[nx, ny] = WDanger;
                                }
                                else if (dCell.meleesWarning + dCell.rangersWarning > 0)
                                {
                                    canContinueField = false;
                                    pathMap[nx, ny] = WDanger;
                                }

                                // проверка занятой клетки
                                if (canContinueField == true)
                                {
                                    int id = cellWithIdAny[nx][ny];
                                    if (id >= 0)// occupied cell
                                    {
                                        if (entityMemories.ContainsKey(id))
                                        {
                                            canContinueField = false;
                                            pathMap[nx, ny] = WBusy;
                                        }
                                        else if (enemiesById.ContainsKey(id))// enemy 
                                        {
                                            canContinueField = false;
                                            pathMap[nx, ny] = WEnemy;
                                        }
                                        else // it is resource
                                        {
                                            canContinueField = false;
                                            pathMap[nx, ny] = WResource;
                                        }
                                    }
                                }

                                if (canContinueField == true) // empty, safe cell or through free unit
                                {
                                    //add weight and findCell
                                    pathMap[nx, ny] = w - 1;
                                    findCells.Add(new XYWeight(nx, ny, w - 1));
                                }
                            }
                            //можем не проверять уже занятые клетки, так как у нас волны распространяются по очереди 1-2-3-4 и т.д.
                        }
                    }
                }
                findCells.RemoveAt(0);
            }
            #endregion

            #region ищем место строительства вблизи
            int findX = -1;
            int findY = -1;
            int findDistToBuilder = 0;

            int x = 0;
            int y = 0;
            bool first = false;
            int maxLine = mapSize - buildingSize + 1; // нет смысла искать в крайних позициях, туда не влезет здание
            for (int line = 0; line < maxLine;)
            {
                // провкера
                if (buildBarrierMap[x, y].CanBuildNow(buildingSize)) // можем здесь построить
                {
                    bool blocked = false;
                    int bx = rangedBasePotencPlace1.X;
                    int by = rangedBasePotencPlace1.Y;
                    if (x >= bx - 1 - buildingSize && x < bx + 6 && y >= by - 1 - buildingSize && y < by + 6)
                        blocked = true;
                    bx = rangedBasePotencPlace2.X;
                    by = rangedBasePotencPlace2.Y;
                    if (x >= bx - 1 - buildingSize && x < bx + 6 && y >= by - 1 - buildingSize && y < by + 6)
                        blocked = true;
                    if (blocked == false)
                    {
                        // есть ли рядом строители?
                        int dist = 0;
                        // обходим здание в поисках ближайшего (max) строителя
                        for (int i = 0; i < buildingSize; i++)
                        {
                            int d1 = GetPathMapValueSafe(pathMap, x - 1, y + i, 0); // left
                            int d2 = GetPathMapValueSafe(pathMap, x + buildingSize, y + i, 0); // right
                            int d3 = GetPathMapValueSafe(pathMap, x + i, y - 1, 0); // down
                            int d4 = GetPathMapValueSafe(pathMap, x + i, y + buildingSize, 0); // up
                            if (d1 > dist) dist = d1;
                            if (d2 > dist) dist = d2;
                            if (d3 > dist) dist = d3;
                            if (d4 > dist) dist = d4;
                        }

                        if (dist > findDistToBuilder)
                        {
                            findX = x;
                            findY = y;
                            findDistToBuilder = dist;
                            if (dist == startWeight) // на соседней клетке строитель
                            {
                                break; // можно не искать дальше
                            }
                            else
                            {
                                int newValue = line + startWeight - dist + buildingSize; // ограничиваем поиск за крайем карты
                                if (newValue < maxLine)
                                    maxLine = newValue; // ищем еще несколько линий и хватит
                            }
                        }
                    }
                }

                // инкремент

                if (first)
                {
                    if (x == line - 1)
                    {
                        x = line;
                        y = 0;
                        first = false;
                    }
                    else
                    {
                        x++;
                    }

                }
                else
                {
                    if (x == y)
                    {
                        line++;
                        x = 0;
                        y = line;
                        first = true;
                    }
                    else
                    {
                        y++;
                    }
                }
            }


            #endregion

            return new Vec2Int(findX, findY);


        }
        int GetPathMapValueSafe(int[,] pathMap, int x, int y, int defaultValue)
        {
            if (x >= 0 && x < mapSize && y >= 0 && y < mapSize)
            {
                return pathMap[x, y];
            }
            else
            {
                return defaultValue;
            }
        }

        Vec2Int FindPositionForRangedBase()
        {
            int buildingSize = properties[EntityType.RangedBase].Size;

            int sx = 15;
            int sy = 15;
            int maxFind = 70;
            int flag = 3;   //  /2  \3
            int dx = 0;     //  \1  /0
            int dy = 0; // with my cell
            //int flag = 0;   //  /2  \3
            //int dx = 1;     //  \1  /0
            //int dy = 0; // without my cell
            for (int step = 0; step <= maxFind;)
            {
                // отмечаем
                int nx = sx + dx;
                int ny = sy + dy;
                if (nx >= 0 && nx < mapSize && ny >= 0 && ny < mapSize)
                {
                    if (buildBarrierMap[nx, ny].CanBuildNow(buildingSize))
                    {
                        return new Vec2Int(nx, ny);
                    }
                }

                //двигаем цель
                if (flag == 0)
                {
                    dx--;
                    dy--;
                    if (dx == 0) flag = 1;
                }
                else if (flag == 1)
                {
                    dx--;
                    dy++;
                    if (dy == 0) flag = 2;
                }
                else if (flag == 2)
                {
                    dx++;
                    dy++;
                    if (dx == 0) flag = 3;
                }
                else if (flag == 3)
                {
                    dx++;
                    dy--;
                    if (dy == 0)
                    {
                        flag = 0;
                        dx++;
                        step++;
                    }
                    else if (dy < 0)// first shift from 0,0
                    {
                        dx = 1;
                        dy = 0;
                        flag = 0;
                        step++;
                    }

                }
            }

            return new Vec2Int(-1, -1);
        }

        class XYWeight
        {
            public int x;
            public int y;
            public int weight;
            public int index;
            public XYWeight(int _x, int _y, int _weight)
            {
                x = _x;
                y = _y;
                weight = _weight;
                index = 0;
            }
            public XYWeight(int _x, int _y, int _weight, int _index)
            {
                x = _x;
                y = _y;
                weight = _weight;
                index = _index;
            }
        }
        
        void AddEnemyDangerCells(int sx, int sy, EntityType entityType)
        {
            if ((entityType == EntityType.BuilderUnit) || (entityType == EntityType.MeleeUnit) || (entityType == EntityType.RangedUnit) || (entityType == EntityType.Turret))
            {
                int range = properties[entityType].Attack.Value.AttackRange;
                int size = properties[entityType].Size;
                int sxRight = sx + size - 1;
                int syUp = sy + size - 1;
                for (int si = 0; si < size; si++)
                {
                    //my base
                    for (int siy = 0; siy < size; siy++)
                    {
                        // сам себе не угрожает
                        //AddEnemyDangerValueToCellSafe(sx + si, sy + siy, entityType, true);
                    }

                    //straight
                    for (int di = 1; di <= range; di++)
                    {
                        AddEnemyDangerValueToCellSafe(sx - di, sy + si, entityType);// left
                        AddEnemyDangerValueToCellSafe(sx + si, sy - di, entityType);// down
                        AddEnemyDangerValueToCellSafe(sxRight + di, sy + si, entityType);// right
                        AddEnemyDangerValueToCellSafe(sx + si, syUp + di, entityType);// up
                    }
                }
                //diagonal quarter
                for (int aa = 1; aa <= range - 1; aa++)
                {
                    for (int bb = 1; bb <= range - aa; bb++)
                    {
                        AddEnemyDangerValueToCellSafe(sx - aa, sy - bb, entityType);//left-down
                        AddEnemyDangerValueToCellSafe(sx - aa, syUp + bb, entityType);//left-up
                        AddEnemyDangerValueToCellSafe(sxRight + aa, syUp + bb, entityType);//right-up
                        AddEnemyDangerValueToCellSafe(sxRight + aa, sy - bb, entityType);//right-down
                    }
                }

                //warning diagonal
                for (int cc = 0; cc <= range; cc++)
                {
                    AddEnemyDangerValueToCellSafe(sx - range - 1 + cc, sy - cc, entityType, false);//left-down
                    AddEnemyDangerValueToCellSafe(sx - cc, syUp + range + 1 - cc, entityType, false);//left-up
                    AddEnemyDangerValueToCellSafe(sxRight + range + 1 - cc, syUp + cc, entityType, false);//right-up
                    AddEnemyDangerValueToCellSafe(sxRight + cc, sy - range - 1 + cc, entityType, false);//right-down
                }
                if (entityType == EntityType.Turret)
                {
                    AddEnemyDangerValueToCellSafe(sx - range - 1, syUp, entityType, false);//left
                    AddEnemyDangerValueToCellSafe(sxRight, syUp + range + 1, entityType, false);//up
                    AddEnemyDangerValueToCellSafe(sxRight + range + 1, sy, entityType, false);//right
                    AddEnemyDangerValueToCellSafe(sx, sy - range - 1, entityType, false);//down
                }
            }
        }
        void AddEnemyDangerValueToCellSafe(int x, int y, EntityType entityType, bool aim = true)
        {
            if (x >= 0 && x < mapSize && y >= 0 && y < mapSize)
            {
                switch (entityType)
                {
                    case EntityType.BuilderUnit:
                        if (aim)
                            enemyDangerCells[x][y].buildersAim++;
                        else
                            enemyDangerCells[x][y].buildersWarning++;
                        break;
                    case EntityType.MeleeUnit:
                        if (aim)
                            enemyDangerCells[x][y].meleesAim++;
                        else
                            enemyDangerCells[x][y].meleesWarning++;
                        break;
                    case EntityType.RangedUnit:
                        if (aim)
                            enemyDangerCells[x][y].rangersAim++;
                        else
                            enemyDangerCells[x][y].rangersWarning++;
                        break;
                    case EntityType.Turret:
                        if (aim)
                            enemyDangerCells[x][y].turretsAim++;
                        break;
                }
            }
        }
        void AddEntityViewToOnceVisibleMap(EntityType entityType, int sx, int sy)
        {
            int sightRange = properties[entityType].SightRange + 1;
            int size = properties[entityType].Size;
            // с этой клетки еще не смотрели по сторонам (благодаря перемещению или новому зданию)
            if (onceVisibleMap[sx][sy] < sightRange)
            {
                int sxRight = sx + size - 1;
                int syUp = sy + size - 1;
                for (int si = 0; si < size; si++)
                {
                    //my base
                    for (int siy = 0; siy < size; siy++)
                    {
                        SetOnceVisibleMapSafe(sx + si, sy + siy, sightRange);
                    }

                    //straight
                    for (int di = 1; di < sightRange; di++)
                    {
                        int value = sightRange - di;
                        SetOnceVisibleMapSafe(sx - di, sy + si, value);// left
                        SetOnceVisibleMapSafe(sx + si, sy - di, value);// down
                        SetOnceVisibleMapSafe(sxRight + di, sy + si, value);// right
                        SetOnceVisibleMapSafe(sx + si, syUp + di, value);// up
                    }
                }
                //diagonal
                for (int aa = 1; aa < sightRange - 1; aa++)
                {
                    for (int bb = 1; bb < sightRange - aa; bb++)
                    {
                        int value = sightRange - aa - bb;
                        SetOnceVisibleMapSafe(sx - aa, sy - bb, value);//left-down
                        SetOnceVisibleMapSafe(sx - aa, syUp + bb, value);//left-up
                        SetOnceVisibleMapSafe(sxRight + aa, syUp + bb, value);//right-up
                        SetOnceVisibleMapSafe(sxRight + aa, sy - bb, value);//right-down
                    }
                }
            }
        }
        void AddEntityViewToCurrentVisibleMap(EntityType entityType, int sx, int sy)
        {
            int sightRange = properties[entityType].SightRange + 1;
            int size = properties[entityType].Size;

            int sxRight = sx + size - 1;
            int syUp = sy + size - 1;
            for (int si = 0; si < size; si++)
            {
                //my base
                for (int siy = 0; siy < size; siy++)
                {
                    SetCurrentVisibleMapSafe(sx + si, sy + siy);
                }

                //straight
                for (int di = 1; di < sightRange; di++)
                {
                    SetCurrentVisibleMapSafe(sx - di, sy + si);// left
                    SetCurrentVisibleMapSafe(sx + si, sy - di);// down
                    SetCurrentVisibleMapSafe(sxRight + di, sy + si);// right
                    SetCurrentVisibleMapSafe(sx + si, syUp + di);// up
                }
            }
            //diagonal
            for (int aa = 1; aa < sightRange - 1; aa++)
            {
                for (int bb = 1; bb < sightRange - aa; bb++)
                {
                    SetCurrentVisibleMapSafe(sx - aa, sy - bb);//left-down
                    SetCurrentVisibleMapSafe(sx - aa, syUp + bb);//left-up
                    SetCurrentVisibleMapSafe(sxRight + aa, syUp + bb);//right-up
                    SetCurrentVisibleMapSafe(sxRight + aa, sy - bb);//right-down
                }
            }
        }
        void SetOnceVisibleMapSafe(int x, int y, int value)
        {
            if (x >= 0 && x < mapSize && y >= 0 && y < mapSize)
            {
                if (onceVisibleMap[x][y] < value)
                    onceVisibleMap[x][y] = value;
            }
        }
        void SetCurrentVisibleMapSafe(int x, int y)
        {
            if (x >= 0 && x < mapSize && y >= 0 && y < mapSize)
            {
                currentVisibleMap[x][y] = true;
            }
        }

        void SaveEntitiesMemory()
        {
            foreach (var e in entityMemories)
            {
                e.Value.SavePrevState();
            }
        }

        Vec2Int FindSpawnPosition(int myX, int myY, bool agressive)
        {
            //find nearest enemy
            int cx = myX + 2;
            int cy = myY + 2;

            Vec2Int targetPos;
            if (agressive)
            {
                targetPos = FindNearestEnemy(cx, cy);
            }
            else
            {
                targetPos = FindNearestResource(cx, cy);
            }

            int tx = 0;
            int ty = 0;

            //find nearest building cell
            int positionDindex = FindBuildingDPositionToTarget(myX, myY, targetPos.X, targetPos.Y);

            //if needed select nearest free 
            for (int i = 0; i < 20; i++)
            {
                int index = positionDindex + buildingPositionIter[i];
                if (index < 0) index += 20;
                if (index >= 20) index -= 20;

                int kx = myX + buildingPositionDX[index];
                int ky = myY + buildingPositionDY[index];

                if (kx >= 0
                    && kx < _playerView.MapSize
                    && ky >= 0
                    && ky < _playerView.MapSize)
                {
                    if (cellWithIdAny[kx][ky] < 0)
                    {
                        tx = kx;
                        ty = ky;
                        break;
                    }
                }
            }
            return new Vec2Int(tx, ty);
        }

        int FindBuildingDPositionToTarget(int bx, int by, int tx, int ty)
        {
            bool xLeft = false;
            bool xMiddle = false;
            bool xRight = false;
            if (tx < bx)
                xLeft = true;
            else if (tx >= bx + largeBuildingSize)
                xRight = true;
            else
                xMiddle = true;

            bool yUp = false;
            bool yDown = false;
            bool yCenter = false;
            if (ty < by)
                yDown = true;
            else if (ty >= by + largeBuildingSize)
                yUp = true;
            else
                yCenter = true;

            int targetD = -1;

            if (yCenter)
            {
                if (xLeft)
                {
                    targetD = ty - by;//0-4
                }
                if (xRight)
                {
                    targetD = 14 - ty + by; //14-10
                }
            }
            if (yDown)
            {
                if (xLeft)
                {
                    targetD = random.Next(2) * 19;//0 or 19
                }
                if (xMiddle)
                {
                    targetD = 19 - tx + bx;//19-15
                }
                if (xRight)
                {
                    targetD = 14 + random.Next(2); //14-15
                }
            }

            if (yUp)
            {
                if (xLeft)
                {
                    targetD = 4 + random.Next(2);//4 or 5
                }
                if (xMiddle)
                {
                    targetD = 5 + tx - bx;//5-9
                }
                if (xRight)
                {
                    targetD = 9 + random.Next(2); //9-10
                }
            }

            if (targetD < 0 || targetD >= 20)
            {
                return random.Next(20);
            }
            else
            {
                return targetD;
            }

        }

        Vec2Int FindNearestResource(int sx, int sy)
        {
            int index = -1;
            int distance = _playerView.MapSize * 3;

            for (int i = 0; i < _playerView.Entities.Length; i++)
            {
                if (_playerView.Entities[i].PlayerId == null)
                {
                    int d = System.Math.Abs(sx - _playerView.Entities[i].Position.X) + System.Math.Abs(sy - _playerView.Entities[i].Position.Y);
                    if (d < distance)
                    {
                        distance = d;
                        index = i;
                    }
                }
            }

            if (index >= 0)
            {
                return _playerView.Entities[index].Position;
            }
            else
            {
                return new Vec2Int(_playerView.MapSize / 2, _playerView.MapSize / 2);
            }
        }
        struct StartAndTargetPoint
        {
            public readonly int startX;
            public readonly int startY;
            public readonly int targetX;
            public readonly int targetY;
            public StartAndTargetPoint(int sx, int sy, int tx, int ty)
            {
                startX = sx;
                startY = sy;
                targetX = tx;
                targetY = ty;
            }
        }
        /// <summary>
        /// ищет ближайший (по пройденным клеткам) к базе ресурс и возвращает ХУ клетки производства и ХУ ресурса
        /// </summary>
        /// <param name="baseX">положение Х базы родителя </param>
        /// <param name="baseY">положение У базы родителя</param>
        /// <returns>  </returns>
        Vec2Int FindNearestToBaseResourceReturnSpawnPlace(int baseX, int baseY)
        {
            //int size = properties[EntityType.BuilderBase].Size;
            //int startIndex = mapSize * mapSize; //стартовое значение, которое будем уменьшать
            //int minIndex = startIndex - maxDistance; //минимальное значение, дальше которого не будем искать

            int tx = 0;
            int ty = 0;
            int maxFindWeight = 0;

            for (int i = 0; i < buildingPositionDX.Length; i++)
            {
                int nx = baseX + buildingPositionDX[i];
                int ny = baseY + buildingPositionDY[i];
                if (nx >= 0 && nx < mapSize && ny >= 0 && ny < mapSize)
                {
                    if (cellWithIdAny[nx][ny] < 0)
                    {
                        int w = resourcePotentialField[nx][ny];
                        if (w > maxFindWeight)
                        {
                            maxFindWeight = w;
                            tx = nx;
                            ty = ny;
                        }
                    }
                }
            }
            return new Vec2Int(tx, ty);
            // странно, как это произошло? 
            // 1) нет места строительства вокруг базы
        }
        Vec2Int FindNearestEnemy(int sx, int sy)
        {
            int enemyIndex = -1;
            int distance = _playerView.MapSize * 3;

            for (int i = 0; i < _playerView.Entities.Length; i++)
            {
                if (_playerView.Entities[i].PlayerId != null)
                {
                    if (_playerView.Entities[i].PlayerId != myId)
                    {
                        int d = System.Math.Abs(sx - _playerView.Entities[i].Position.X) + System.Math.Abs(sy - _playerView.Entities[i].Position.Y);
                        if (d < distance)
                        {
                            distance = d;
                            enemyIndex = i;
                        }
                    }
                }
            }

            if (enemyIndex >= 0)
            {
                return _playerView.Entities[enemyIndex].Position;
            }
            else
            {
                if (isFinal)
                {
                    return new Vec2Int(70, 70);
                }
                else
                {
                    if (_playerView.CurrentTick < 500)
                        return new Vec2Int(73, 7);
                    else if (_playerView.CurrentTick < 750)
                        return new Vec2Int(73, 73);
                    else return new Vec2Int(7, 73);
                }
                //return new Vec2Int(_playerView.MapSize / 2, _playerView.MapSize / 2);
            }
        }
        bool IsFreeCellsRange(int x1, int y1, int x2, int y2, bool onlyBuildings = false, bool ignoreBorder = false)
        {
            //xy1 always <= xy2
            if (x2 < x1) { int k = x1; x1 = x2; x2 = k; }
            if (y2 < y1) { int k = y1; y1 = y2; y2 = k; }

            //check map border
            if (ignoreBorder)
            {
                if (x1 < 0) x1 = 0;
                if (y1 < 0) y1 = 0;
                if (x2 < 0) x2 = 0;
                if (y2 < 0) y2 = 0;
                if (x1 >= _playerView.MapSize) x1 = _playerView.MapSize - 1;
                if (y1 >= _playerView.MapSize) y1 = _playerView.MapSize - 1;
                if (x2 >= _playerView.MapSize) x2 = _playerView.MapSize - 1;
                if (y2 >= _playerView.MapSize) y2 = _playerView.MapSize - 1;
            }
            else
            {
                if (x1 < 0
                    || y1 < 0
                    || x2 < 0
                    || y2 < 0
                    || x1 >= _playerView.MapSize
                    || y1 >= _playerView.MapSize
                    || x2 >= _playerView.MapSize
                    || y2 >= _playerView.MapSize)
                {
                    return false;
                }
            }

            if (onlyBuildings)
            {
                for (int x = x1; x <= x2; x++)
                {
                    for (int y = y1; y <= y2; y++)
                    {
                        if (cellWithIdOnlyBuilding[x][y] >= 0) return false;
                    }
                }
            }
            else
            {
                for (int x = x1; x <= x2; x++)
                {
                    for (int y = y1; y <= y2; y++)
                    {
                        if (cellWithIdAny[x][y] >= 0) return false;
                    }
                }
            }
            return true;
        }

        bool TryFindSpawnPlace(ref int sx, ref int sy, int size, bool horizontal)//horizontal is [[[]]]
        {
            //check map borders
            if (sx < 0
                || sy < 0
                || sx >= _playerView.MapSize
                || sy >= _playerView.MapSize)
            {
                return false;
            }

            //check first line
            if (!IsFreeCellsRange(sx, sy, (horizontal) ? sx : (sx + size - 1), (horizontal) ? (sy + size - 1) : sy))
            {
                return false;
            }


            //find minimun
            int minimum = 0;
            for (int i = 1; i < size; i++)
            {
                if (horizontal)
                {
                    if (!IsFreeCellsRange(sx - i, sy, sx - i, sy + size - 1))
                        break;
                }
                else
                {
                    if (!IsFreeCellsRange(sx, sy - i, sx + size - 1, sy - i))
                        break;
                }
                minimum = -i;
            }


            //check
            if ((1 - minimum) == size)
            {
                //sx;
                //sy;
                return true;
            }

            //find maximum
            for (int i = 1; i < size; i++)
            {
                if (horizontal)
                {
                    if (!IsFreeCellsRange(sx + i, sy, sx + i, sy + size - 1)) break;
                }
                else
                {
                    if (!IsFreeCellsRange(sx, sy + i, sx + size - 1, sy + i)) break;
                }

                if ((1 + i - minimum) == size)
                {
                    if (horizontal)
                    {
                        sx += minimum;
                    }
                    else
                    {
                        sy += minimum;
                    }
                    return true;
                }
            }

            //                                      xo2yo2
            //          *       *       *    xi2yi2         maxDY = 2
            //          *       *       *       *             |
            //          *       *       *       *    pos      |
            //       xi1xi2     *       *       *           minDY = -1
            // xo1yo1
            // inner - almost free cells
            // outer - without buildings (can be units)

            return false;
        }

        void CountNumberOfEntitiesAndMap()
        {            
            //clear map
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    cellWithIdAny[x][y] = -1;
                    cellWithIdOnlyBuilding[x][y] = -1;
                }
            }

            //save previous number of entities
            foreach (var p in previousEntityCount)
            {
                previousEntityCount[p.Key] = currentMyEntityCount[p.Key];
            }

            //zeroize current enity count
            foreach (var ent in entityTypesArray)
            {
                currentMyEntityCount[ent] = 0;
            }


            populationMax = 0;

            //count current entities
            foreach (var entity in _playerView.Entities)
            {
                //fill freeCell arraies for map of moving
                int size = properties[entity.EntityType].Size;
                int x1 = entity.Position.X;
                int x2 = x1 + size - 1;
                int y1 = entity.Position.Y;
                int y2 = y1 + size - 1;
                bool canMove = properties[entity.EntityType].CanMove;
                int id = entity.Id;
                for (int x = x1; x <= x2; x++)
                {
                    for (int y = y1; y <= y2; y++)
                    {
                        //this is building
                        if (!canMove)
                            cellWithIdOnlyBuilding[x][y] = id;
                        //this is any enitites
                        cellWithIdAny[x][y] = id;
                    }
                }

                if (entity.PlayerId == myId)
                {
                    //count number of my enitites
                    currentMyEntityCount[entity.EntityType]++;
                    // calc max population
                    if (entity.Active)
                        populationMax += properties[entity.EntityType].PopulationProvide;
                }
            }
            //calc max and current population
            populationUsing = 0;
            foreach (var e in properties)
            {
                populationUsing += currentMyEntityCount[e.Key] * e.Value.PopulationUse;
            }

            //theoreticaly same population using
            //unitCount = currentMyEntityCount[EntityType.BuilderUnit] + currentMyEntityCount[EntityType.MeleeUnit] + currentMyEntityCount[EntityType.RangedUnit];
        }
        static int MinWithoutZero(int a, int b)
        {
            if (a == 0)
                return b;
            if (b == 0)
                return a;
            return a < b ? a : b;
        }
        static int Min(int a, int b)
        {
            return a < b ? a : b;
        }
        static int Abs(int p)
        {
            if (p >= 0)
                return p;
            else
                return -p;

        }
        public void DrawLineOnce(float x1, float y1, float x2, float y2, Color color1, Color color2)
        {
            if (debugOptions[(int)DebugOptions.canDrawGetAction])
            {
                ColoredVertex[] vertices = new ColoredVertex[] {
                    new ColoredVertex(new Vec2Float(x1,y1), new Vec2Float(), color1),
                    new ColoredVertex(new Vec2Float(x2,y2), new Vec2Float(), color2),
                };
                DebugData.Primitives lines = new DebugData.Primitives(vertices, PrimitiveType.Lines);
                _debugInterface.Send(new DebugCommand.Add(lines));
            }
        }
        public void DrawQuad(float x1, float y1, float x2, float y2, Color color)
        {
            ColoredVertex[] vertices = new ColoredVertex[] {
                    new ColoredVertex(new Vec2Float(x1,y1), new Vec2Float(), color),
                    new ColoredVertex(new Vec2Float(x1,y2), new Vec2Float(), color),
                    new ColoredVertex(new Vec2Float(x1,y2), new Vec2Float(), color),
                    new ColoredVertex(new Vec2Float(x2,y2), new Vec2Float(), color),
                    new ColoredVertex(new Vec2Float(x2,y2), new Vec2Float(), color),
                    new ColoredVertex(new Vec2Float(x2,y1), new Vec2Float(), color),
                    new ColoredVertex(new Vec2Float(x2,y1), new Vec2Float(), color),
                    new ColoredVertex(new Vec2Float(x1,y1), new Vec2Float(), color),
                };
            DebugData.Primitives lines = new DebugData.Primitives(vertices, PrimitiveType.Lines);
            _debugInterface.Send(new DebugCommand.Add(lines));
        }

        static Color colorWhite = new Color(1, 1, 1, 1);
        static Color colorMagenta = new Color(1, 0, 1, 1);
        static Color colorRed = new Color(1, 0, 0, 1);
        static Color colorBlack = new Color(0, 0, 0, 1);
        static Color colorGreen = new Color(0, 1, 0, 1);
        static Color colorBlue = new Color(0, 0, 1, 1);
        static Color colorYellow = new Color(0.8f, 0.5f, 0, 1);
         
        public void DebugUpdate(PlayerView playerView, DebugInterface debugInterface)
        {
            if (debugOptions[(int)DebugOptions.canDrawDebugUpdate] == true)
            {
                debugInterface.Send(new DebugCommand.Clear());
                DebugState debugState = debugInterface.GetState();
                debugInterface.Send(new DebugCommand.SetAutoFlush(false));

                if (playerView.Players[0].Id == playerView.MyId)
                {
                    #region draw CPA
                    string text = $"{howMuchResourcesCollectAll} all";
                    int textSize = 16;
                    int margin = 2;
                    int sx = debugState.WindowSize.X - 10;
                    int sy = debugState.WindowSize.Y - 100;
                    int index = 0;
                    int col1 = 27;
                    int col2 = 55;
                    DebugData.PlacedText cpa = new DebugData.PlacedText(new ColoredVertex(null, new Vec2Float(sx, sy - index * (textSize + margin)), colorGreen), text, 1, textSize);
                    debugInterface.Send(new DebugCommand.Add(cpa));
                    index++;
                    cpa = new DebugData.PlacedText(new ColoredVertex(null, new Vec2Float(sx, sy - index * (textSize + margin)), colorGreen), "add / bui / cpa", 1, textSize);
                    debugInterface.Send(new DebugCommand.Add(cpa));

                    for (int i = 0; i < howMuchResourcesCollectLastNTurns.Length; i++)
                    {
                        index++;
                        debugInterface.Send(new DebugCommand.Add(
                            new DebugData.PlacedText(new ColoredVertex(null, new Vec2Float(sx - col2, sy - index * (textSize + margin)), colorGreen),
                            howMuchResourcesCollectLastNTurns[i] + " / ", 1, textSize)));
                        debugInterface.Send(new DebugCommand.Add(
                            new DebugData.PlacedText(new ColoredVertex(null, new Vec2Float(sx - col1, sy - index * (textSize + margin)), colorGreen),
                            howMuchLiveBuildersLast10Turns[i] + " / ", 1, textSize)));
                        debugInterface.Send(new DebugCommand.Add(
                            new DebugData.PlacedText(new ColoredVertex(null, new Vec2Float(sx, sy - index * (textSize + margin)), colorGreen),
                            howMuchResourcesCollectCPALastNTurns[i] + "", 1, textSize)));
                    }
                    #endregion

                    #region draw unvisible resources
                    int currentTick = playerView.CurrentTick - 1;
                    for (int x = 0; x < mapSize; x++)
                    {
                        for (int y = 0; y < mapSize; y++)
                        {
                            if (resourceMemoryMap[x][y] > 0 && resourceMemoryMap[x][y] <= currentTick)
                            {
                                ColoredVertex[] vertices = new ColoredVertex[] {
                            new ColoredVertex(new Vec2Float(x, y), new Vec2Float(), colorBlue),
                            new ColoredVertex(new Vec2Float(x+1,y+1), new Vec2Float(), colorBlue)
                        };
                                DebugData.Primitives lines = new DebugData.Primitives(vertices, PrimitiveType.Lines);
                                debugInterface.Send(new DebugCommand.Add(lines));
                            }
                        }
                    }
                    #endregion

                    #region draw danger level
                    bool showTurretsZone = false;
                    bool showBuildersZone = false;
                    bool showMeleesZone = false;
                    bool showRangersZone = false;

                    for (int x = 0; x < mapSize; x++)
                    {
                        for (int y = 0; y < mapSize; y++)
                        {
                            if (showRangersZone)
                            {
                                if (enemyDangerCells[x][y].rangersAim > 0)
                                {
                                    ColoredVertex position = new ColoredVertex(new Vec2Float(x, y), new Vec2Float(0, 0), colorBlack);
                                    debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, enemyDangerCells[x][y].rangersAim.ToString(), 0, 12)));
                                }
                                if (enemyDangerCells[x][y].rangersWarning > 0)
                                {
                                    ColoredVertex position = new ColoredVertex(new Vec2Float(x, y + 0.5f), new Vec2Float(0, 0), colorRed);
                                    debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, enemyDangerCells[x][y].rangersWarning.ToString(), 0, 12)));
                                }
                            }
                            if (showTurretsZone)
                            {
                                if (enemyDangerCells[x][y].turretsAim > 0)
                                {
                                    ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y + 0.3f), new Vec2Float(0, 0), colorBlack);
                                    debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, enemyDangerCells[x][y].turretsAim.ToString(), 0.5f, 12)));
                                }
                            }
                            if (showMeleesZone)
                            {
                                if (enemyDangerCells[x][y].meleesAim > 0)
                                {
                                    ColoredVertex position = new ColoredVertex(new Vec2Float(x + 1, y), new Vec2Float(0, 0), colorBlack);
                                    debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, enemyDangerCells[x][y].meleesAim.ToString(), 1f, 12)));
                                }
                                if (enemyDangerCells[x][y].meleesWarning > 0)
                                {
                                    ColoredVertex position = new ColoredVertex(new Vec2Float(x + 1, y + 0.5f), new Vec2Float(0, 0), colorRed);
                                    debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, enemyDangerCells[x][y].meleesWarning.ToString(), 1f, 12)));
                                }
                            }
                            if (showBuildersZone)
                            {
                                if (enemyDangerCells[x][y].buildersAim > 0)
                                {
                                    ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y), new Vec2Float(0, 0), colorGreen);
                                    debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, enemyDangerCells[x][y].buildersAim.ToString(), 1f, 12)));
                                }
                                if (enemyDangerCells[x][y].buildersWarning > 0)
                                {
                                    ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y + 0.5f), new Vec2Float(0, 0), colorMagenta);
                                    debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, enemyDangerCells[x][y].buildersWarning.ToString(), 1f, 12)));
                                }
                            }
                        }
                    }

                    #endregion

                    #region draw debugLines
                    foreach (var li in debugLines)
                    {
                        ColoredVertex[] vertices = new ColoredVertex[] {
                        new ColoredVertex(new Vec2Float(li._x1 ,li._y1), new Vec2Float(), li._color1),
                        new ColoredVertex(new Vec2Float(li._x2, li._y2), new Vec2Float(), li._color2),
                    };
                        DebugData.Primitives lines = new DebugData.Primitives(vertices, PrimitiveType.Lines);
                        debugInterface.Send(new DebugCommand.Add(lines));
                    }
                    #endregion

                    #region draw resource potential field
                    bool drawResourcePotentialField = false;
                    int maxXY = mapSize / 2;
                    if (drawResourcePotentialField)
                    {
                        int maxWeight = mapSize * mapSize;
                        for (int x = 0; x < maxXY; x++)
                        {
                            for (int y = 0; y < maxXY; y++)
                            {
                                int weight = resourcePotentialField[x][y];
                                if (weight == RPFdangerCellWeight)
                                {
                                    ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y + 0.3f), new Vec2Float(0, 0), colorRed);
                                    debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, "x", 0.5f, 14)));
                                }
                                else if (weight == RPFwarningCellWeight)
                                {
                                    ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y + 0.3f), new Vec2Float(0, 0), colorMagenta);
                                    debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, "!", 0.5f, 14)));
                                }
                                else if (weight == RPFdeniedBuilderWeight)
                                {
                                    ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y + 0.3f), new Vec2Float(0, 0), colorGreen);
                                    debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, "v", 0.5f, 14)));
                                }
                                else if (weight == RPFenemyEntityWeight)
                                {
                                    ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y + 0.3f), new Vec2Float(0, 0), colorWhite);
                                    debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, "-", 0.5f, 14)));
                                }
                                else if (weight == RPFmyBuildingWeight)
                                {
                                    ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y + 0.3f), new Vec2Float(0, 0), colorWhite);
                                    debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, "-", 0.5f, 14)));
                                }
                                else if (weight == 0)
                                {
                                    ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y + 0.3f), new Vec2Float(0, 0), colorBlack);
                                    debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, "x", 0.5f, 14)));
                                }
                                else if (weight < maxWeight)
                                {
                                    ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y + 0.3f), new Vec2Float(0, 0), colorBlue);
                                    debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, (maxWeight - weight).ToString(), 0.5f, 14)));
                                }

                            }
                        }
                    }

                    #endregion
                }
                #region примеры использования
                //if (playerView.CurrentTick == 10)
                //{
                //    debugInterface.Send(new DebugCommand.Add(new DebugData.Log("Тестовое сообщение")));

                //ColoredVertex position = new ColoredVertex(new Vec2Float(10, 10), new Vec2Float(0, 0), colorGreen);
                //    debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, "Ghbdtn", 0, 16)));

                //    ColoredVertex[] vertices = new ColoredVertex[] {
                //        new ColoredVertex(new Vec2Float(7,7), new Vec2Float(), colorRed),
                //        new ColoredVertex(new Vec2Float(17,7), new Vec2Float(), colorRed),
                //        new ColoredVertex(new Vec2Float(20,20), new Vec2Float(), colorRed),
                //        new ColoredVertex(new Vec2Float(10,10), new Vec2Float(), colorRed)
                //    };
                //    DebugData.Primitives lines = new DebugData.Primitives(vertices, PrimitiveType.Lines);
                //    debugInterface.Send(new DebugCommand.Add(lines));
                //}
                #endregion
                debugInterface.Send(new DebugCommand.Flush());
            }
        }

    }
    
    enum EntityOrders { none, spawnUnit, buildNow, buildGo, repairGo, pushed, tryRetreat, canRetreat, attack, moveAndAttack, collect, move, cancelAll }
    class EntityMemory
    {
        public Group group { get; private set; }
        public int prevHealth;
        public Vec2Int prevPosition;
        public int myId;
        public int? targetId;
        public Vec2Int targetPos;
        public Vec2Int movePos;
        public bool moveBreakThrough;
        public bool moveFindClosestPosition;
        public AutoAttack? autoAttack;
        public EntityType targetEntityType;
        public EntityOrders order { get; set; }
        public EntityOrders prevOrder;
        public bool optimized;

        public Entity myEntity { get; private set; }

        public bool checkedNow;

        //EntityMemory()
        //{
        //    group = null;
        //    prevHealth = -1;
        //    prevPosition = new Vec2Int(-1, -1);
        //    myId = -1;
        //    checkedNow = false;
        //}

        public EntityMemory(Entity entity)
        {
            group = null;
            prevHealth = entity.Health;
            prevPosition = entity.Position;
            myId = entity.Id;
            myEntity = entity;
            checkedNow = true;
            moveFindClosestPosition = false;
            moveBreakThrough = false;
            autoAttack = new AutoAttack();
            targetId = null;
            targetPos = new Vec2Int(-1, -1);
            movePos = new Vec2Int(-1, -1);
            order = EntityOrders.none;
        }
        public void Update(Entity entity)
        {
            //order = EntityOrders.none;
            optimized = false;
            checkedNow = true;
            myEntity = entity;
        }
        public void OrderTryRetreat(bool opt)
        {
            order = EntityOrders.tryRetreat;
            optimized = opt;
        }
        public void OrderCanRetreat(Vec2Int moveP, bool breakThrough, bool findClosestPosition, bool opt)
        {
            order = EntityOrders.canRetreat;
            optimized = opt;
            movePos = moveP;
            moveBreakThrough = breakThrough;
            moveFindClosestPosition = findClosestPosition;
        }
        public void OrderMove(Vec2Int moveP, bool breakThrough, bool findClosestPosition, bool opt)
        {
            order = EntityOrders.move;
            optimized = opt;
            movePos = moveP;
            moveBreakThrough = breakThrough;
        }
        public void OrderPushed(Vec2Int moveP, bool breakThrough, bool findClosestPosition, bool opt)
        {
            order = EntityOrders.pushed;
            optimized = opt;
            movePos = moveP;
            moveBreakThrough = breakThrough;
            moveFindClosestPosition = findClosestPosition;
        }
        public void OrderAttack(int? tarId, AutoAttack? autoAt, bool opt)
        {
            order = EntityOrders.attack;
            targetId = tarId;
            autoAttack = autoAt;
            optimized = opt;
        }
        public void OrderMoveAndAttack(Vec2Int moveP, bool breakThrough, bool findClosestPosition, int? tarId, AutoAttack? autoAt, bool opt)
        {
            order = EntityOrders.moveAndAttack;

            movePos = moveP;
            moveBreakThrough = breakThrough;
            moveFindClosestPosition = findClosestPosition;

            targetId = tarId;
            autoAttack = autoAt;
            optimized = opt;
        }
        public void OrderBuild(Vec2Int targetP, EntityType type, Vec2Int moveP, bool breakThrough, bool findClosestPosition, bool opt)
        {
            order = EntityOrders.buildNow;
            optimized = opt;
            movePos = moveP;
            moveBreakThrough = breakThrough;
            moveFindClosestPosition = findClosestPosition;
            targetPos = targetP;
            targetEntityType = type;
        }
        public void OrderGoToBuild(Vec2Int moveP, bool breakThrough, bool findClosestPosition, bool opt)
        {
            order = EntityOrders.buildGo;
            optimized = opt;
            movePos = moveP;
            moveBreakThrough = breakThrough;
            moveFindClosestPosition = findClosestPosition;
        }
        public void OrderRepairGo(int tarId, Vec2Int moveP, bool breakThrough, bool findClosestPosition, bool opt)
        {
            order = EntityOrders.repairGo;
            optimized = opt;
            movePos = moveP;
            moveBreakThrough = breakThrough;
            moveFindClosestPosition = findClosestPosition;
            targetId = tarId;
        }
        public void ResetTarget()
        {
            targetId = null;
            targetPos = new Vec2Int(-1, -1);
            movePos = new Vec2Int(-1, -1);
            moveBreakThrough = false;
            moveFindClosestPosition = false;
            autoAttack = new AutoAttack();
            order = EntityOrders.none;
        }
        public void SetGroup(Group g)
        {
            if (group != null)
            {
                group.RemoveMember(myId);
            }
            group = g;
            group.AddMember(myId);
        }

        public void SavePrevState()
        {
            prevOrder = order;
            prevHealth = myEntity.Health;
            prevPosition = myEntity.Position;
        }
        public void Die()
        {
            if (group != null)
            {
                group.RemoveMember(myId);
            }
        }
    }
    class Group
    {

        public List<int> members { get; private set; }

        public Group()
        {
            members = new List<int>();
        }

        public void AddMember(int m)
        {
            if (!members.Contains(m))
            {
                members.Add(m);
            }
        }

        public void RemoveMember(int m)
        {
            if (members.Contains(m))
            {
                members.Remove(m);
            }
        }
    }
}
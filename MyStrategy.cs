using Aicup2020.Model;
using System.Collections.Generic;

namespace Aicup2020
{
    public class MyStrategy
    {
        Dictionary<int, EntityMemory> entityMemories = new Dictionary<int, EntityMemory>();

        #region Служебные переменные
        EntityType[] entityTypesArray = { EntityType.BuilderUnit, EntityType.RangedUnit, EntityType.MeleeUnit, 
            EntityType.Turret, EntityType.House, EntityType.BuilderBase, EntityType.MeleeBase, EntityType.RangedBase, EntityType.Wall, 
            EntityType.Resource };

        bool needPrepare = true;
        #endregion

        #region клетки где можно построить юнита вокруг здания
        int largeBuildingSize = 5;
        int[] buildingPositionDX = {-1, -1, -1, -1, -1, 0, 1, 2, 3 ,4, 5, 5, 5, 5, 5,  4,  3,  2,  1,  0 };
        int[] buildingPositionDY = { 0,  1,  2,  3,  4, 5, 5, 5, 5, 5, 4, 3, 2, 1, 0, -1, -1, -1, -1, -1 };
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

        enum DebugOptions { canDrawGetAction, drawBuildBarrierMap, drawBuildAndRepairOrder, drawBuildAndRepairPath, drawRetreat, 
            drawPotencAttack, drawOptAttack,
            canDrawDebugUpdate, allOptionsCount }
        bool[] debugOptions = new bool[(int)DebugOptions.allOptionsCount];

        PlayerView _playerView;
        DebugInterface _debugInterface;
        public IDictionary<EntityType, EntityProperties> properties;

        System.Random random = new System.Random();

        int[][] cellWithIdOnlyBuilding;
        int[][] cellWithIdAny;
        int[][] nextPositionMyUnitsMap;
        int[][] onceVisibleMap;
        bool[][] currentVisibleMap;
        int[][] resourceMemoryMap;
        int[][] resourcePotentialField;
        const int RPFmyBuildingWeight = -10;
        const int RPFdangerCellWeight = -6;
        const int RPFenemyEntityWeight = -2;
        const int RPFdeniedBuilderWeight = 1;
        const int RPFwarningCellWeight = 2;
        class BuildMapCell
        {
            public bool s2canBuildNow;
            public bool s2canBuildAfter;
            public bool s2noBaseOrWarriorBarrier;
            public bool s2noBuilderBarrier;
            public bool s2noEnemiesBarrier;
            public int s2howManyResBarrier;

            public bool s3canBuildNow;
            public bool s3canBuildAfter;
            public bool s3noBaseOrWarriorBarrier;
            public bool s3noBuilderBarrier;
            public bool s3noEnemiesBarrier;
            public int s3howManyResBarrier;

            public bool s5canBuildNow;
            public bool s5canBuildAfter;
            public bool s5noBaseOrWarriorBarrier;
            public bool s5noBuilderBarrier;
            public bool s5noEnemiesBarrier;
            public int s5howManyResBarrier;

            public BuildMapCell()
            {
                Reset();
            }

            public void Check()
            {
                if (s2noBaseOrWarriorBarrier
                        && s2noEnemiesBarrier
                        && s2howManyResBarrier == 0)
                {
                    if (s2noBuilderBarrier)
                    {
                        s2canBuildNow = true;
                        s2canBuildAfter = true;
                    }                             
                    else
                        s2canBuildAfter = true;
                }
                if (s3noBaseOrWarriorBarrier
                    && s3noEnemiesBarrier
                    && s3howManyResBarrier == 0)
                {
                    if (s3noBuilderBarrier)
                    {
                        s3canBuildNow = true;
                        s3canBuildAfter = true;
                    }
                    else
                        s3canBuildAfter = true;
                }
                if (s5noBaseOrWarriorBarrier
                    && s5noEnemiesBarrier
                    && s5noBuilderBarrier
                    && s5howManyResBarrier == 0)
                {
                    if (s5noBuilderBarrier)
                    {
                        s5canBuildNow = true;
                        s5canBuildAfter = true;
                    }
                    else
                        s5canBuildAfter = true;
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
            }

            public int HowManyResBarrier (int size)
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

            public bool CanBuildNow (int size)
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
        BuildMapCell[,] buildBarrierMap;
        List<Vec2Int> preSetHousePositions;
        bool preSetHousePlacingComplete = false;

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

        class PotencAttackCell
        {
            public int rangersAim;
            public int rangersWarning;
            public int meleesAim;
            public int meleesWarning;
            public int turretsAim;
            public bool drawn;
            public PotencAttackCell()
            {
                Reset();
            }
            public void Reset()
            {
                rangersAim = rangersWarning = meleesAim = meleesWarning = turretsAim = 0;
                drawn = false;
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
                get {
                    if (x >= 0 && x < _mapSize && y >= 0 && y < _mapSize)
                        return _potencAttackMap[x, y];
                    else
                        return null;
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
            public void AddCell(int x, int y, EntityType entityType, bool aim = true)
            {
                if (x >= 0 && x < _mapSize && y >= 0 && y < _mapSize)
                {
                    switch (entityType)
                    {
                        //case EntityType.BuilderUnit:
                        //    if (aim)
                        //        _potencAttackMap[x, y].buildersAim++;
                        //    else
                        //        enemyDangerCells[x][y].buildersWarning++;
                        //    break;
                        case EntityType.MeleeUnit:
                            if (aim)
                                _potencAttackMap[x, y].meleesAim++;
                            else
                                _potencAttackMap[x, y].meleesWarning++;
                            break;
                        case EntityType.RangedUnit:
                            if (aim)
                                _potencAttackMap[x, y].rangersAim++;
                            else
                                _potencAttackMap[x, y].rangersWarning++;
                            break;
                        case EntityType.Turret:
                            if (aim)
                                _potencAttackMap[x, y].turretsAim++;
                            break;
                    }
                }
            }
            public bool TryDraw(int x, int y)
            {
                if (x >= 0 && x < _mapSize && y >= 0 && y < _mapSize)
                {
                    return _potencAttackMap[x, y].TryDraw();
                }
                return false;
            }
        }
        PotencAttackMap potencAttackMap;
        

        int builderCountForStartBuilding = 3; // количество ближайших свободных строителей которое ищется при начале строительства
        float startBuildingFindDistanceFromHealth = 0.4f; // дистанция поиска строителей как процент здоровья 

        #region Статичстические переменные
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
        
        int myResources;
        int myScore;
        int myId;
        int mapSize;
        int populationMax = 0;
        int populationUsing = 0;
        bool fogOfWar;
        #endregion
        #region Желания, Планы, Намерения и т.д.

        enum DesireType {WantCreateBuilders, WantCreateRangers,
            WantCreateHouses, WantCreateRangerBase,
            WantRepairBuildings,
            WantCollectResources, WantRetreatBuilders,
            WantTurretAttacks, WantAllWarriorsAttack };
        List<DesireType> desires = new List<DesireType>();
        List<DesireType> prevDesires = new List<DesireType>();
        
        enum PlanType {PlanCreateBuilders, PlanCreateRangers,
            PlanCreateHouses, PlanCreateRangerBase,
            PlanRepairNewBuildings, PlanRepairOldBuildings,
            PlanExtractResources, PlanRetreatBuilders,
            PlanTurretAttacks, PlanAllWarriorsAttack }
        List<PlanType> plans = new List<PlanType>();
        List<PlanType> prevPlans = new List<PlanType>();
        
        enum IntentionType { IntentionCreateBuilder, IntentionStopCreatingBuilder, 
            IntentionCreateRanger, IntentionStopCreatingRanger,
            IntentionCreateHouse,  IntentionCreateRangedBase,
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
            public DebugLine(float x1, float y1, float x2, float y2, Color color1, Color color2 )
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
            GenerateResourcePotentialField();
            GenerateBuildBarrierMap();
            GeneratePotencAttackMap();

            iHaveActiveRangedBase = false;
            foreach (var id in basicEntityIdGroups[EntityType.RangedBase].members)
            {
                if (entityMemories[id].myEntity.Active == true)
                {
                    iHaveActiveRangedBase = true;
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
                if (debugOptions[(int)DebugOptions.drawPotencAttack] == true)
                    DrawPotencMap(3);

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
            debugOptions[(int)DebugOptions.drawBuildAndRepairOrder] = false;
            debugOptions[(int)DebugOptions.drawBuildAndRepairPath] = false;
            debugOptions[(int)DebugOptions.drawPotencAttack] = false;
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

            potencAttackMap = new PotencAttackMap(mapSize);
            buildBarrierMap = new BuildMapCell[mapSize, mapSize];
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    buildBarrierMap[x, y] = new BuildMapCell();
                }
            }
            #endregion

            if (!fogOfWar)
            {
                for(int x = 0; x <mapSize; x++)
                {
                    for(int y = 0; y < mapSize; y++)
                    {
                        onceVisibleMap[x][y] = 20;
                        currentVisibleMap[x][y] = true;
                    }
                }                
            }

            preSetHousePositions = new List<Vec2Int>();
            preSetHousePositions.Add(new Vec2Int(2, 2));
            preSetHousePositions.Add(new Vec2Int(5, 2));
            preSetHousePositions.Add(new Vec2Int(8, 2));
            preSetHousePositions.Add(new Vec2Int(2, 5));
            preSetHousePositions.Add(new Vec2Int(2, 8));


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
            enemiesById.Clear();
            // zero enemy danger cells
            for (var x = 0; x < mapSize; x++) {
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

                        //check my builder units
                        if (properties[em.myEntity.EntityType].CanMove == false)
                        {
                            for (int i = 0; i < intentions.Count; i++)
                            {
                                if (intentions[i].intentionType == IntentionType.IntentionCreateHouse)
                                {
                                    if (intentions[i].targetPosition.X == em.myEntity.Position.X && intentions[i].targetPosition.Y == em.myEntity.Position.Y)
                                    {
                                        intentions[i].targetId = em.myEntity.Id;
                                        break;
                                    }
                                }
                            }
                        }
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
                    resourceMemoryMap[e.Position.X][e.Position.Y] = currentTick;
                } else // it's enemy
                {
                    enemiesById.Add(e.Id, e);
                    AddEnemyDangerCells(e.Position.X, e.Position.Y, e.EntityType);
                }
            }

            //remove died entity
            foreach (var m in entityMemories)
            {
                if (m.Value.checkedNow == false)
                {
                    m.Value.Die();
                    entityMemories.Remove(m.Key);
                }
            }

            //statistics
            if (_playerView.CurrentTick == 0)
            {
                howMuchResourcesCollectLastTurn = 0;
            }
            howMuchResourcesCollectAll += howMuchResourcesCollectLastTurn;
            for(int i = howMuchResourcesCollectCPALastNTurns.Length - 1; i > 0; i--)
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
                        if (resourceMemoryMap[x][y] > 0 && resourceMemoryMap[x][y] < currentTick)
                            resourceMemoryMap[x][y] = 0;
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
                            } else if (dCell.meleesWarning + dCell.rangersWarning > 0)
                            {
                                canContinueField = false;
                                resourcePotentialField[nx][ny] = RPFwarningCellWeight;
                                //findCells.Add(new XYWeight(nx, ny, RPFwarningCellWeight));
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
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    buildBarrierMap[x, y].Reset();
                }
            }

            // check barriers
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    int id = cellWithIdAny[x][y];
                    if (id >= 0)
                    {
                        for (int k = x - 4; k <=x; k++)
                        {
                            for (int m = y - 4; m <= y; m++)
                            {
                                bool s2 = (x - k < 2 && y - m < 2);
                                bool s3 = (x - k < 3 && y - m < 3);
                                bool s5 = true; // (x - k < 5 && y - m < 5);

                                if (k >= 0 && m >= 0) // больше mapSize никогда не станет
                                {
                                    if (entityMemories.ContainsKey(id))
                                    {
                                        if (entityMemories[id].myEntity.EntityType == EntityType.BuilderUnit)
                                        {
                                            if (s2) buildBarrierMap[k, m].s2noBuilderBarrier = false;
                                            if (s3) buildBarrierMap[k, m].s3noBuilderBarrier = false;
                                            if (s5) buildBarrierMap[k, m].s5noBuilderBarrier = false;
                                        } else
                                        {
                                            if (s2) buildBarrierMap[k, m].s2noBaseOrWarriorBarrier = false;
                                            if (s3) buildBarrierMap[k, m].s3noBaseOrWarriorBarrier = false;
                                            if (s5) buildBarrierMap[k, m].s5noBaseOrWarriorBarrier = false;
                                        }

                                    } else if (enemiesById.ContainsKey(id))
                                    {
                                        if (s2) buildBarrierMap[k, m].s2noEnemiesBarrier = false;
                                        if (s3) buildBarrierMap[k, m].s3noEnemiesBarrier = false;
                                        if (s5) buildBarrierMap[k, m].s5noEnemiesBarrier = false;

                                    } else // it's resource
                                    {
                                        if (s2) buildBarrierMap[k, m].s2howManyResBarrier++;
                                        if (s3) buildBarrierMap[k, m].s3howManyResBarrier++;
                                        if (s5) buildBarrierMap[k, m].s5howManyResBarrier++;
                                    }

                                    if (resourceMemoryMap[k][m] > 0)
                                    {
                                        if (s2) buildBarrierMap[k, m].s2howManyResBarrier++;
                                        if (s3) buildBarrierMap[k, m].s3howManyResBarrier++;
                                        if (s5) buildBarrierMap[k, m].s5howManyResBarrier++;
                                    }
                                }
                            }
                        }
                    }
                }
            }            

            // calc can build now
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    BuildMapCell cell = buildBarrierMap[x, y];
                    if (fogOfWar == true) { 
                        if (onceVisibleMap[x][y] > 0)
                        {
                            cell.Check();
                        }                        
                    } else
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
                        if (buildBarrierMap[x,y].s5canBuildAfter == true)
                        {
                            ColoredVertex position = new ColoredVertex(new Vec2Float(x + 0.5f, y + 0.3f), new Vec2Float(0, 0),
                                (buildBarrierMap[x, y].s5canBuildNow) ? colorBlue : colorMagenta);
                            _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, "5", 0.5f, 16)));
                        } else if (buildBarrierMap[x, y].s3canBuildAfter == true)
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
                 _debugInterface.Send(new DebugCommand.Flush());
            }
        }
        void GeneratePotencAttackMap()
        {
            potencAttackMap.Reset();

            foreach(var p in enemiesById)
            {
                EntityType entityType = p.Value.EntityType;
                if (entityType == EntityType.MeleeUnit || entityType == EntityType.RangedUnit)
                {
                    for (int rludc = 0; rludc < 5; rludc++)
                    {
                        int attackRange = properties[entityType].Attack.Value.AttackRange;
                        int size = properties[entityType].Size;
                        int sx = p.Value.Position.X;
                        int sy = p.Value.Position.Y;
                        if (rludc == 0) sx++;
                        else if (rludc == 1) sx--;
                        else if (rludc == 2) sy++;
                        else if (rludc == 3) sy--;
                        // rludc == 4 - is center cell. sc, sy, not changed

                        if (sx >= 0 && sx < mapSize && sy >= 0 && sy < mapSize)
                        {
                            if (cellWithIdOnlyBuilding[sx][sy] < 0) // нельзя ходить в здания и ресурсы
                            {
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
                                    for (int di = 1; di <= attackRange; di++)
                                    {
                                        //potencAttackMap.AddCell(sx - di, sy + si, entityType);
                                        potencAttackMap.AddCell(sx - di, sy + si, entityType);// left
                                        potencAttackMap.AddCell(sx + si, sy - di, entityType);// down
                                        potencAttackMap.AddCell(sxRight + di, sy + si, entityType);// right
                                        potencAttackMap.AddCell(sx + si, syUp + di, entityType);// up
                                    }
                                }
                                //diagonal quarter
                                for (int aa = 1; aa <= attackRange - 1; aa++)
                                {
                                    for (int bb = 1; bb <= attackRange - aa; bb++)
                                    {
                                        potencAttackMap.AddCell(sx - aa, sy - bb, entityType);//left-down
                                        potencAttackMap.AddCell(sx - aa, syUp + bb, entityType);//left-up
                                        potencAttackMap.AddCell(sxRight + aa, syUp + bb, entityType);//right-up
                                        potencAttackMap.AddCell(sxRight + aa, sy - bb, entityType);//right-down
                                    }
                                }

                                //warning diagonal
                                for (int cc = 0; cc <= attackRange; cc++)
                                {
                                    potencAttackMap.AddCell(sx - attackRange - 1 + cc, sy - cc, entityType, false);//left-down
                                    potencAttackMap.AddCell(sx - cc, syUp + attackRange + 1 - cc, entityType, false);//left-up
                                    potencAttackMap.AddCell(sxRight + attackRange + 1 - cc, syUp + cc, entityType, false);//right-up
                                    potencAttackMap.AddCell(sxRight + cc, sy - attackRange - 1 + cc, entityType, false);//right-down
                                }
                            }
                        }
                    }
                } 
                else if (entityType == EntityType.Turret)
                {
                    int attackRange = properties[entityType].Attack.Value.AttackRange;
                    int size = properties[entityType].Size;
                    int sx = p.Value.Position.X;
                    int sy = p.Value.Position.Y;
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
                        for (int di = 1; di <= attackRange; di++)
                        {                            
                            potencAttackMap.AddCell(sx - di, sy + si, entityType);// left
                            potencAttackMap.AddCell(sx + si, sy - di, entityType);// down
                            potencAttackMap.AddCell(sxRight + di, sy + si, entityType);// right
                            potencAttackMap.AddCell(sx + si, syUp + di, entityType);// up
                        }
                    }
                    //diagonal quarter
                    for (int aa = 1; aa <= attackRange - 1; aa++)
                    {
                        for (int bb = 1; bb <= attackRange - aa; bb++)
                        {
                            potencAttackMap.AddCell(sx - aa, sy - bb, entityType);//left-down
                            potencAttackMap.AddCell(sx - aa, syUp + bb, entityType);//left-up
                            potencAttackMap.AddCell(sxRight + aa, syUp + bb, entityType);//right-up
                            potencAttackMap.AddCell(sxRight + aa, sy - bb, entityType);//right-down
                        }
                    }

                    //warning diagonal
                    for (int cc = 0; cc <= attackRange; cc++)
                    {
                        potencAttackMap.AddCell(sx - attackRange - 1 + cc, sy - cc, entityType, false);//left-down
                        potencAttackMap.AddCell(sx - cc, syUp + attackRange + 1 - cc, entityType, false);//left-up
                        potencAttackMap.AddCell(sxRight + attackRange + 1 - cc, syUp + cc, entityType, false);//right-up
                        potencAttackMap.AddCell(sxRight + cc, sy - attackRange - 1 + cc, entityType, false);//right-down
                    }
                    
                    potencAttackMap.AddCell(sx - attackRange - 1, syUp, entityType, false);//left
                    potencAttackMap.AddCell(sxRight, syUp + attackRange + 1, entityType, false);//up
                    potencAttackMap.AddCell(sxRight + attackRange + 1, sy, entityType, false);//right
                    potencAttackMap.AddCell(sx, sy - attackRange - 1, entityType, false);//down                    
                }
            }
        }
        void DrawPotencMap(int dist)
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
                    int dy = 0; 
                    for (int step = 0; step <= dist;)
                    {
                        //рисуем
                        int nx = sx + dx;
                        int ny = sy + dy;
                        if (potencAttackMap.TryDraw(nx, ny))
                        {
                            PotencAttackCell cell = potencAttackMap[nx, ny];
                            int sumAim = cell.meleesAim + cell.rangersAim + cell.turretsAim;
                            int sumWarning = cell.meleesWarning + cell.rangersWarning;
                            int textSize = 16;
                            if (sumAim > 0 && sumWarning > 0)
                            {
                                ColoredVertex position = new ColoredVertex(new Vec2Float(nx + 0.45f, ny + 0.2f), new Vec2Float(0, 0), colorRed);
                                _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, sumAim.ToString(), 1f, textSize)));
                                position = new ColoredVertex(new Vec2Float(nx + 0.55f, ny + 0.2f), new Vec2Float(0, 0), colorBlack);
                                _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, sumWarning.ToString(), 0, textSize)));
                            }
                            else if (sumAim > 0)
                            {
                                ColoredVertex position = new ColoredVertex(new Vec2Float(nx + 0.5f, ny + 0.2f), new Vec2Float(0, 0), colorRed);
                                _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, sumAim.ToString(), 0.5f, textSize)));
                            } else if (sumWarning > 0)
                            {
                                ColoredVertex position = new ColoredVertex(new Vec2Float(nx + 0.5f, ny + 0.2f), new Vec2Float(0, 0), colorBlack);
                                _debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, sumWarning.ToString(), 0.5f, textSize)));
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
                            } else if (dy < 0)// first shift from 0,0
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

        void GenerateDesires()
        {
            prevDesires.Clear();
            prevDesires = desires;
            desires = new List<DesireType>();

            #region Хочу строить базу лучников
            bool needMakeRangedBase = false;
            if (basicEntityIdGroups[EntityType.RangedBase].members.Count == 0)
            {
                if (_playerView.CurrentTick > 200)
                {
                    for (int x = 0; x < mapSize; x++)
                    {
                        for (int y = 0; y < mapSize; y++)
                        {
                            if (buildBarrierMap[x, y].CanBuildNow(5))
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
            } else
            {
                if (entityMemories[basicEntityIdGroups[EntityType.RangedBase].members[0]].myEntity.Active == false)
                {
                    needMakeRangedBase = true;
                }

            }
            
            #endregion


            #region Хочу строить дома
            if (needMakeRangedBase == false)
            {
                int[] popMax = new int[] { 15, 30, 55, 70, 100, 1000 };
                int[] popRange = new int[] { 0, 4, 8, 10, 15, 20 };
                for (int i = 0; i < popMax.Length; i++)
                {
                    if (populationMax <= popMax[i])
                    {
                        if (populationUsing + popRange[i] >= populationMax)
                        {
                            desires.Add(DesireType.WantCreateHouses);
                            break;
                        }
                    }
                }
            }
            #endregion

            #region Выбираем кого строить
            int countEnemiesOnMyTerritory = 0;
            int myTerritoryX = mapSize / 2;
            int myTerritoryY = mapSize / 2;
            foreach(var p in enemiesById)
            {
                if (p.Value.Position.X < myTerritoryX && p.Value.Position.Y < myTerritoryY)
                {
                    countEnemiesOnMyTerritory++;
                }
            }

            bool needCreateWarriors = false;
            if (iHaveActiveRangedBase == true)
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
                    } else if (currentMyEntityCount[EntityType.BuilderUnit] > 70)
                    {
                        desires.Add(DesireType.WantCreateRangers);
                    }
                    else
                    {
                        if (currentMyEntityCount[EntityType.BuilderUnit] < currentMyEntityCount[EntityType.RangedUnit] * 2)
                            desires.Add(DesireType.WantCreateBuilders);
                        else
                            desires.Add(DesireType.WantCreateRangers);
                    }
                }
            } else // нет базы лучников
            {
                if (populationUsing < populationMax)
                    desires.Add(DesireType.WantCreateBuilders);
            }
            #endregion

            desires.Add(DesireType.WantRetreatBuilders);
            desires.Add(DesireType.WantCollectResources);

            desires.Add(DesireType.WantTurretAttacks);
            desires.Add(DesireType.WantAllWarriorsAttack);

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
                                foreach(var ni in prevIntentions)
                                {
                                    if (ni.intentionType == IntentionType.IntentionCreateHouse) 
                                        count++;
                                }
                                foreach (var ni in intentions)
                                {
                                    if (ni.intentionType == IntentionType.IntentionCreateHouse)
                                        count++;
                                }
                                if ((populationMax <= 30 && count == 0) || (populationMax <= 60 && count <= 2) || (count <= 4))
                                {
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
                                 plans.Add(PlanType.PlanCreateRangerBase);                                
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
                    case DesireType.WantAllWarriorsAttack:
                        #region хочу чтобы все войны атаковали
                        //i have warrior
                        if ((currentMyEntityCount[EntityType.RangedUnit] + currentMyEntityCount[EntityType.MeleeUnit]) > 0)
                        {
                            plans.Add(PlanType.PlanAllWarriorsAttack);
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
                            Vec2Int pos = FindPositionForHouse();
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
                    case PlanType.PlanAllWarriorsAttack:
                        intentions.Add(new Intention(IntentionType.IntentionAllWarriorsAttack, basicEntityIdGroups[EntityType.MeleeUnit]));
                        intentions.Add(new Intention(IntentionType.IntentionAllWarriorsAttack, basicEntityIdGroups[EntityType.RangedUnit]));
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
                    case IntentionType.IntentionCreateHouse:
                        #region Создаем намерение на ремонт построенного или отменяем строительство
                        //if (prevIntentions[i].targetId >= 0)
                        //{
                        //    // удачное строительство, создавем намерение на ремонт
                        //    Intention intention = new Intention(IntentionType.IntentionRepairNewBuilding, prevIntentions[i].targetId);
                        //    intention.targetGroup = prevIntentions[i].targetGroup;
                        //    intentions.Add(intention);
                        //}
                        //else
                        //{
                        //    // неудачное строительство, распускаем группу
                        //    while (prevIntentions[i].targetGroup.members.Count > 0)
                        //    {
                        //        int id = prevIntentions[i].targetGroup.members[0];
                        //        entityMemories[id].SetGroup(basicEntityIdGroups[entityMemories[id].myEntity.EntityType]);
                        //    }
                        //}
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
                            entityMemories[id].OrderTryRetreat(); 
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
            OptimizeOrderToAttackRM(); // оптимизируем приказы на атаку воинов
            // OptimizeOrderToHealWarriors(); // оптимизируем приказы на лечение воинов
            OptimizeOrderToRetreat(); // оптимизируем отступление

            OptimizeOrderToRepairNew(); // ремонтируем новые здания
            OptimizeOrderToBuildNew(); // строим новые здания
            OptimizeOrderToRepairOld(); // ремонтируем старые здания

            
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
        void OptimizeOrderToAttackRM()
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
            foreach(var en in enemiesById)
            {
                if (en.Value.EntityType == EntityType.RangedUnit)
                {
                   // enemyRangersId.Add(en.Key);
                    enemyRangers.Add(en.Key, new EnemyToOpt(en.Key, en.Value.EntityType, en.Value.Health, en.Value.Position.X, en.Value.Position.Y));
                } else if (en.Value.EntityType == EntityType.MeleeUnit)
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
            foreach(var i in myRangers)
            {
                if (i.Value.Count == 0)
                    deleteKeys.Add(i.Key);
            }
            foreach(var i in deleteKeys)
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
                                entityMemories[i.Key].OrderAttack(i.Value[0]._me._id, null, true);
                                // draw attack line
                                if (debugOptions[(int)DebugOptions.drawOptAttack])
                                {
                                    DrawLineOnce(
                                        entityMemories[i.Key].myEntity.Position.X + 0.3f,
                                        entityMemories[i.Key].myEntity.Position.Y + 0.5f,
                                        i.Value[0]._me._x + 0.3f,
                                        i.Value[0]._me._y + 0.5f,
                                        colorBlack,
                                        colorBlack);
                                }
                                // damage health
                                if (enemyMelees.ContainsKey(i.Value[0]._me._id))
                                {
                                    enemyMelees[i.Value[0]._me._id]._me._health -= damageR;
                                    if (enemyMelees[i.Value[0]._me._id]._me._health <= 0)
                                        wasKilled = true;
                                }
                                else
                                if (enemyRangers.ContainsKey(i.Value[0]._me._id))
                                {
                                    enemyRangers[i.Value[0]._me._id]._me._health -= damageR;
                                    if (enemyRangers[i.Value[0]._me._id]._me._health <= 0)
                                        wasKilled = true;
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
                        } else if (enemyRangers.ContainsKey(targets[t]))
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
                for(int i = 0; i < attackers.Count; i++)
                {
                    foreach(var en in myRangers[attackers[i]])
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
                    } else if (enemyRangers.ContainsKey(targets[i]))
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
                                entityMemories[id].OrderAttack(enemyMelees[targets[0]]._me._id, null, true);
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
                            }
                        }
                        else
                        {                            
                            foreach (var id in attackers)
                            {                                
                                if (myRangers[id][0]._targetsMyUnitsById[id]._dist <= 2)// retreat nearest
                                {
                                    entityMemories[id].OrderTryRetreat();
                                }
                                else // fire another
                                {
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
                            foreach(var id in attackers)
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
                            entityMemories[attackers[kk]].OrderAttack(enemyId, null, true);
                            if (debugOptions[(int)DebugOptions.drawOptAttack])
                            {
                                DrawLineOnce(
                                    entityMemories[attackers[kk]].myEntity.Position.X + 0.3f,
                                    entityMemories[attackers[kk]].myEntity.Position.Y + 0.5f,
                                    enemiesById[enemyId].Position.X + 0.3f,
                                    enemiesById[enemyId].Position.Y + 0.5f,
                                    colorMagenta,
                                    colorMagenta);
                            }
                            if (enemyMelees.ContainsKey(enemyId))
                            {
                                enemyMelees[enemyId]._me._health -= damageR;

                            }
                            else if (enemyRangers.ContainsKey(enemyId))
                            {
                                enemyRangers[enemyId]._me._health -= damageR;
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

            #region определяем движение остальных стрелков

            #endregion

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
                    if (arrayPair[a,t] == true)
                    {
                        if (a == 0)
                        {
                            int[] v = new int[sizeA]; // создаем первые варианты
                            v[a] = t;
                            variants.Add(v);
                        } else
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
                foreach(var t in variants[i])
                {
                    damage[t]++;
                }
                int kills = 0;
                for (int t = 0; t <sizeT; t++)
                {
                    if (targetsHealth[t] == damage[t])
                        kills++;
                }
                killsArray.Add(kills);
            }
            // выбираем лучшие варианты
            int max = 0;
            foreach(var n in killsArray)
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
                } else
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
                                                            } else
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
                _debugInterface.Send(new DebugCommand.Flush());
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
                    || intentions[ni].intentionType == IntentionType.IntentionCreateRangedBase) {
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

            if (intention.intentionType == IntentionType.IntentionCreateHouse) // == различие - проверяем начинку только для стройки нового здания
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
                    minWeight = startWeight - planDistance / 2;
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
                    for (int i = 0; i < findCells.Count;i++)
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
                                        pathMap[nx, ny].weight = WNextPosition;
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
                                                            DrawLineOnce(nx + 0.5f, ny + 0.5f, fx+0.5f, fy +0.5f, colorMagenta, colorMagenta);
                                                        }
                                                        switch (intention.intentionType)// == difference
                                                        {
                                                            case IntentionType.IntentionCreateRangedBase:
                                                            case IntentionType.IntentionCreateHouse:
                                                                entityMemories[id].OrderGoToBuild(new Vec2Int(fx, fy), true, true, true);
                                                                break;
                                                            case IntentionType.IntentionRepairNewBuilding:
                                                            case IntentionType.IntentionRepairOldBuilding:
                                                                entityMemories[id].OrderRepairGo(intention.targetId, new Vec2Int(fx, fy), true, true, true);
                                                                break;
                                                            default: throw new System.Exception("Неизвестное намерение");
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
                                                        minWeight = startWeight - planDistance / 2;
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

            foreach(var em in entityMemories)
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
                    case EntityOrders.attackAndMove:
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
        //void OrderStartCreateBuilding(int id, Vec2Int posBuild, Vec2Int movePos, EntityType type)
        //{
        //    entityMemories[id].order = EntityOrders.buildNow;
        //    entityMemories[id].movePos = movePos;// new Vec2Int(posBuild.X + properties[type].Size / 2, posBuild.Y + properties[type].Size);
        //    entityMemories[id].moveBreakThrough = false;
        //    entityMemories[id].moveFindClosestPosition = true;

        //    entityMemories[id].targetEntityType = type;
        //    entityMemories[id].targetPos = posBuild;     
        //}
        //void OrderRepairBuilding(int id, int targetId)
        //{
            
        //    entityMemories[id].order = EntityOrders.repairGo;
        //    entityMemories[id].movePos = entityMemories[targetId].myEntity.Position;
        //    entityMemories[id].moveBreakThrough = false;
        //    entityMemories[id].moveFindClosestPosition = true;
        //    entityMemories[id].targetId = targetId;

        //    ////repair
        //    //MoveAction moveAction = new MoveAction();
        //    //moveAction.BreakThrough = false;
        //    //moveAction.FindClosestPosition = true;
        //    //moveAction.Target = entityMemories[targetId].myEntity.Position;

        //    //RepairAction repairAction = new RepairAction(targetId);
        //    //actions.Add(id, new EntityAction(moveAction, null, null, repairAction));                                
        //}
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

            entityMemories[id].order = EntityOrders.attackAndMove;
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

        //int[,] FindPathMapForBuild(int sx, int sy, EntityType type)
        //{
        //    int[,] pathMap = new int[mapSize, mapSize];
        //    bool canBuildHere = true;

        //    int size = properties[type].Size;
        //    int buildPoints = properties[type].MaxHealth;

        //    //стартовое значение, которое будем уменьшать
        //    int startWeight = mapSize * mapSize;
        //    int minWeight = startWeight - buildPoints;
        //    int WInside = 2;
        //    int WBuilding = -1;
        //    int WEnemy = -2;
        //    int WResource = -3;
        //    int WDanger = -10;

        //    List<int> borderMansId = new List<int>();
        //    //добавляем стартовые клетки поиска вокруг места строительства + юнитов на границе + отмечаем непроходимые клетки (здания, ресурсы, враги)
        //    List<XYWeight> findCells = new List<XYWeight>();
        //    for (int m = 0; m < size; m++)
        //    {
        //        for (int h = 0; h < 4; h++)
        //        {
        //            int fx = sx;
        //            int fy = sy;
        //            if (h == 0){
        //                fx = sx + m;
        //                fy = sy + size;
        //            } else if (h == 1)
        //            {
        //                fx = sx + m;
        //                fy = sy - 1;
        //            } else if (h == 2)
        //            {
        //                fx = sx - 1;
        //                fy = sy + m;
        //            } else if (h == 3)
        //            {
        //                fx = sx + size;
        //                fy = sy + m;
        //            }
        //            if (fx >= 0 && fx < mapSize && fy >= 0 && fy < mapSize) {
        //                int id = cellWithIdAny[fx][fy];
        //                if (id >= 0)
        //                {
        //                    if (entityMemories.ContainsKey(id))
        //                    {
        //                        if (properties[entityMemories[id].myEntity.EntityType].CanMove)// только юниты, здания здесь не нужны
        //                        { 
        //                            findCells.Add(new XYWeight(fx, fy, startWeight));
        //                            pathMap[fx, fy] = startWeight;
        //                            borderMansId.Add(id); // учитываем тех кто стоит на границе места строительства в том числе Могут быть войны
        //                        } else
        //                        {
        //                            pathMap[fx, fy] = WBuilding;
        //                        }
        //                    } else if (enemiesById.ContainsKey(id))
        //                    {
        //                        pathMap[fx, fy] = WEnemy;
        //                    } else
        //                    {
        //                        pathMap[fx, fy] = WResource;
        //                    }       
        //                } else
        //                {
        //                    findCells.Add(new XYWeight(fx, fy, startWeight));
        //                    pathMap[fx, fy] = startWeight;
        //                }
        //            }
        //        }
        //    }

        //    // определяем сколько юнитов нам мешают строить здание 
        //    // это могут быть только строители - нельзя начинать строить там где стоят войны, 
        //    // но они могут быть на границе с местом строительства
        //    List<int> insidersId = new List<int>();
        //    for (int cx = sx; cx < sx + size; cx++)
        //    {
        //        for ( int cy = sy; cy < sy+size; cy++)
        //        {
        //            pathMap[cx, cy] = WInside;
        //            if (cellWithIdAny[cx][cy] >= 0)
        //            { 
        //                // учитываем тех кто стоит внутри места строительства, 
        //                // !!!!!! считаем что войнов, зданий и врагов тут не может быть, по рпавилам выбора места строительства, только строители
        //                insidersId.Add(cellWithIdAny[cx][cy]);
        //            }
        //        }
        //    }

        //    // объединяем стартовые ячейки в группы, у каждой группы соседей теперь должен быть одинаковый индекс (порядок номеров не важен, могут быть пропуски)
        //    int lastIndex = 1; // 0 используется у пустых групп
        //    for (int iter = 0; iter < findCells.Count; iter++)
        //    {
        //        int myIndex = findCells[iter].index;
        //        if (myIndex == 0)
        //        {
        //            myIndex = lastIndex;
        //            lastIndex++;
        //        }

        //        // ищем всех соседей и проверяем их индекс
        //        int mx = findCells[iter].x;
        //        int my = findCells[iter].y;
        //        for (int i = iter + 1; i < findCells.Count; i++)
        //        {
        //            int dist = Abs(mx - findCells[i].x) + Abs(my - findCells[i].y);
                    
        //            if (dist == 1)// это мой сосед
        //            {
        //                if (findCells[i].index == 0)
        //                {
        //                    findCells[i].index = myIndex;
        //                } else
        //                {
        //                    // это старший брат, надо взять его индекс себе и всем кому уже присвоили мой индекс
        //                    int newIndex = findCells[i].index;
        //                    for (int n = 0; n < findCells.Count; n++)
        //                    {
        //                        if (findCells[n].index == myIndex)
        //                        {
        //                            findCells[n].index = newIndex;
        //                        }
        //                    }
        //                    myIndex = newIndex;
        //                }
        //            }
        //        }
        //    }         

        //    // counter
        //    int startCellCount = findCells.Count;
        //    int startInsidersCount = insidersId.Count;
        //    int startBorderMansCount = borderMansId.Count;
        //    List<int> outsidersId = new List<int>();
        //    if (startInsidersCount > 0) // надо выгонять внутренних парней
        //    {
        //        if (startInsidersCount + startBorderMansCount > startCellCount) // надо выталкивать людей наружу, им не хватает места внутри
        //        {
        //            // значит здесь строить нельзя
        //            canBuildHere = false;
        //            #region закомментированный код
        //            //int diff = startInsidersCount + startBorderMansCount - startCellCount; //количество юнитов, которым не хватает места
        //            //bool stop = false;
        //            //int currentWeight = startWeight;
        //            //// проводим расширение зоны поиска НАРУЖУ, пока не найдем достаточное количество клеток или не упремся в то что все не поместятся
        //            //while(stop == false)
        //            //{
        //            //    for (int i = 0; i < findCells.Count; i++)
        //            //    {
        //            //        if (findCells[i].weight < currentWeight) // перешли к следующей группе удаленности от старта, надо провеирть хватает ли места
        //            //        {
        //            //            if (startInsidersCount + startBorderMansCount + outsidersId.Count >= i)
        //            //            { // у нас достаточно клеток, чтобы вместить в себя всех
        //            //                stop = true;
        //            //                break;
        //            //            } else
        //            //            { // у нас недостаточно клеток, значит проводим новую волну поиска
        //            //                currentWeight = findCells[i].weight;
        //            //            }
        //            //        } else // делаем поиск по соседям
        //            //        {
        //            //            int tx = findCells[i].x;
        //            //            int ty = findCells[i].y;
        //            //            int tw = findCells[i].weight;
        //            //            int ti = findCells[i].index;

        //            //            // проверяем есть ли в нас тут тот кого надо попросить подвинуться
        //            //            if (tw != startWeight) // не проверяемстартовые клетки, тамошние учтены в bordersMansId
        //            //            {
        //            //                int hereId = cellWithIdAny[tx][ty];
        //            //                if (hereId >= 0)
        //            //                {
        //            //                    if (entityMemories.ContainsKey(hereId))
        //            //                    {
        //            //                        if (properties[entityMemories[hereId].myEntity.EntityType].CanMove)
        //            //                        {
        //            //                            outsidersId.Add(hereId);
        //            //                        }
        //            //                    }
        //            //                }
        //            //            }

        //            //            // проверяем соседние клетки
        //            //            for (int jj = 0; jj < 4; jj++)
        //            //            {
        //            //                int nx = tx;
        //            //                int ny = ty;
        //            //                if (jj == 0) nx--;
        //            //                if (jj == 1) ny--;
        //            //                if (jj == 2) nx++;
        //            //                if (jj == 3) ny++;

        //            //                if (nx >= 0 && nx < mapSize && ny >= 0 && ny < mapSize)
        //            //                {
        //            //                    if (pathMap[nx,ny] == 0) // поиск наружу, поэтому в WInside мы не смотрим
        //            //                    {
        //            //                        bool canContinue = true;

        //            //                        // проверка опасной зоны - не ищем в опасных клетках
        //            //                        var dCell = enemyDangerCells[nx][ny];
        //            //                        if (dCell.meleesAim + dCell.rangersAim + dCell.turretsAim > 0)
        //            //                        {
        //            //                            canContinue = false;
        //            //                            pathMap[nx, ny] = WDanger;
        //            //                        }
        //            //                        else if (dCell.meleesWarning + dCell.rangersWarning > 0)
        //            //                        {
        //            //                            canContinue = false;
        //            //                            pathMap[nx, ny] = WDanger;
        //            //                        }

        //            //                        // проверка занятой клетки
        //            //                        if (canContinue == true)
        //            //                        {
        //            //                            int id = cellWithIdAny[nx][ny];
        //            //                            if (id >= 0)// occupied cell
        //            //                            {
        //            //                                if (entityMemories.ContainsKey(id))
        //            //                                {
        //            //                                    if (properties[ entityMemories[id].myEntity.EntityType].CanMove == true)// это юнит?
        //            //                                    {
        //            //                                        ; // все нормально клетку поиска добавят
        //            //                                    }
        //            //                                    else
        //            //                                    {//is my building                                                            
        //            //                                        canContinue = false;
        //            //                                        pathMap[nx, ny] = WBuilding;
        //            //                                    }
        //            //                                }
        //            //                                else if (enemiesById.ContainsKey(id)) // enemy 
        //            //                                {
        //            //                                    canContinue = false;
        //            //                                    pathMap[nx, ny] = WEnemy;
        //            //                                } else // resource
        //            //                                {
        //            //                                    canContinue = false;
        //            //                                    pathMap[nx, ny] = WResource;
        //            //                                }
        //            //                            }
        //            //                        }

        //            //                        if (canContinue == true) // empty, safe cell or through free unit
        //            //                        {
        //            //                            //add weight and findCell
        //            //                            if (tw > minWeight)
        //            //                            {
        //            //                                pathMap[nx, ny] = tw - 1;
        //            //                                findCells.Add(new XYWeight(nx, ny, tw - 1, ti));
        //            //                            }
        //            //                        }
        //            //                    }
        //            //                    //можем не проверять уже занятые клетки, так как у нас волны распространяются по очереди 1-2-3-4 и т.д.
        //            //                }
        //            //            }
        //            //        }
        //            //    }
        //            //    if (stop == false)// мы проверили все клетки, последняя проверка и если клеток не хватает, то строить здесь нельзя.
        //            //    {

        //            //        needFindOutsiders = false;


        //            //        stop = true;
        //            //    }
        //            //}
        //            #endregion
        //        }
        //        else // места внутренним парням хватит
        //        {


        //        }
        //    }

        //    if (canBuildHere == true)
        //    {
        //        if (startInsidersCount + startBorderMansCount < startCellCount) // надо звать строителей снаружи на границу здания - если клеток не хватает, то нет смысла 
        //        {
        //            // на каждом новом парне считаем стоит ли продолжать дальше или найдено достаточно людей

        //            // объединяем в группы стартовые ячейки

        //            // ищем путь до строителей - надеемся что строители существуют=) - строим путь по карте воспоминаний, а не видимости


        //        }
        //    }

            


        //    while (findCells.Count > 0)
        //    {
        //        int bx = findCells[0].x;
        //        int by = findCells[0].y;
        //        int w = findCells[0].weight;

        //        for (int jj = 0; jj < 4; jj++)
        //        {
        //            int nx = bx;
        //            int ny = by;
        //            if (jj == 0) nx--;
        //            if (jj == 1) ny--;
        //            if (jj == 2) nx++;
        //            if (jj == 3) ny++;

        //            if (nx >= 0 && nx < mapSize && ny >= 0 && ny < mapSize)
        //            {
        //                if (resourcePotentialField[nx][ny] == 0)
        //                {
        //                    bool canContinueField = true;

        //                    // проверка опасной зоны
        //                    var dCell = enemyDangerCells[nx][ny];
        //                    if (dCell.meleesAim + dCell.rangersAim + dCell.turretsAim > 0)
        //                    {
        //                        canContinueField = false;
        //                        resourcePotentialField[nx][ny] = RPFdangerCellWeight;
        //                    }
        //                    else if (dCell.meleesWarning + dCell.rangersWarning > 0)
        //                    {
        //                        canContinueField = false;
        //                        resourcePotentialField[nx][ny] = RPFwarningCellWeight;
        //                        //findCells.Add(new XYWeight(nx, ny, RPFwarningCellWeight));
        //                    }

        //                    // проверка занятой клетки
        //                    if (canContinueField == true)
        //                    {
        //                        int id = cellWithIdAny[nx][ny];
        //                        if (id >= 0)// occupied cell
        //                        {
        //                            if (entityMemories.ContainsKey(id))
        //                            {
        //                                if (entityMemories[id].myEntity.EntityType == EntityType.BuilderUnit)
        //                                {
        //                                    if (w == startWeight)//check my builder на соседней клетке с ресурсомs
        //                                    {
        //                                        canContinueField = false;
        //                                        resourcePotentialField[nx][ny] = RPFdeniedBuilderWeight;
        //                                    }
        //                                }
        //                                else
        //                                {
        //                                    if (properties[entityMemories[id].myEntity.EntityType].CanMove == false)//is my building
        //                                    {
        //                                        canContinueField = false;
        //                                        resourcePotentialField[nx][ny] = RPFmyBuildingWeight;
        //                                    }
        //                                }
        //                            }
        //                            else // enemy 
        //                            {
        //                                if (enemiesById.ContainsKey(id))
        //                                {
        //                                    canContinueField = false;
        //                                    resourcePotentialField[nx][ny] = RPFenemyEntityWeight;
        //                                }
        //                            }
        //                        }
        //                    }

        //                    if (canContinueField == true) // empty, safe cell or through free unit
        //                    {
        //                        //add weight and findCell
        //                        resourcePotentialField[nx][ny] = w - 1;
        //                        if (w > minWeight)
        //                            findCells.Add(new XYWeight(nx, ny, w - 1));
        //                    }
        //                }
        //                //можем не проверять уже занятые клетки, так как у нас волны распространяются по очереди 1-2-3-4 и т.д.
        //            }
        //        }
        //        //findCells.RemoveAt(0);
        //    }

        //    return pathMap;
        //}
        bool[] FindAvailableTargetType(int sx, int sy, int size, int range)
        {
            bool[] availableType = new bool[entityTypesArray.Length];
            foreach(var i in entityTypesArray)
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
            } else
            {
                return false;
            }
        }

        int Abs(int p)
        {
            if (p >= 0)
                return p;
            else
                return -p;

        }
        Vec2Int FindPositionForHouse()
        {
            int buildingSize = properties[EntityType.House].Size;

            // check house preset
            if (preSetHousePlacingComplete == false)
            {
                foreach(var v in preSetHousePositions)
                {
                    if (buildBarrierMap[v.X, v.Y].CanBuildNow(buildingSize))
                    {
                        return new Vec2Int(v.X, v.Y);
                    }
                }
                preSetHousePlacingComplete = true;
            }

            // find optimal position

            foreach (var id in basicEntityIdGroups[EntityType.BuilderUnit].members)
            {
                Vec2Int pos = entityMemories[id].myEntity.Position;
                bool posFinded = false;

                //left 
                int x = pos.X - buildingSize;
                int y = pos.Y;
                if (TryFindSpawnPlace(ref x, ref y, buildingSize, false))
                {
                    posFinded = true;
                }
                else
                {
                    //down
                    x = pos.X;
                    y = pos.Y - buildingSize;
                    if (TryFindSpawnPlace(ref x, ref y, buildingSize, true))
                    {
                        posFinded = true;
                    }
                    else
                    {
                        //right
                        x = pos.X + 1;
                        y = pos.Y;
                        if (TryFindSpawnPlace(ref x, ref y, buildingSize, false))
                        {
                            posFinded = true;
                        }
                        else
                        {
                            //up
                            x = pos.X;
                            y = pos.Y + 1;
                            if (TryFindSpawnPlace(ref x, ref y, buildingSize, true))
                            {
                                posFinded = true;
                            }
                        }
                    }
                }

                if (posFinded)
                {
                    return new Vec2Int(x, y);
                    //entityMemories[id].SetGroup(houseBuilderGroup);
                    //entityMemories[id].SetTargetPos(new Vec2Int(x, y));
                    //entityMemories[id].SetMovePos(entityMemories[id].myEntity.Position);
                    //entityMemories[id].SetTargetEntityType(EntityType.House);
                    //break;
                }
            }
            return new Vec2Int(-1, -1);
        }

        Vec2Int FindPositionForRangedBase()
        {
            int buildingSize = properties[EntityType.RangedBase].Size;

            int y = 0;
            for (int x = 12; x < mapSize;)
            {
                if (buildBarrierMap[x, y].CanBuildNow(buildingSize))
                {
                    return new Vec2Int(x, y);
                }

                x--;
                y++;
                if (x < 0)
                {
                    x = y;
                    y = 0;
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
        //List<int> FindFreeNearestBuilders(Vec2Int target, int size, int builderCount, int maxDistance)
        //{
        //    List<int> list = new List<int>();

        //    if (builderCount > basicEntityIdGroups[EntityType.BuilderUnit].members.Count)
        //    {
        //        builderCount = basicEntityIdGroups[EntityType.BuilderUnit].members.Count;
        //    }

        //    if (builderCount == 0) 
        //        return list;

        //    int[][] map = new int[mapSize][];
        //    for (int i = 0; i < mapSize; i++)
        //    {
        //        map[i] = new int[mapSize];
        //    }

        //    int startIndex = mapSize * mapSize; //стартовое значение, которое будем уменьшать
        //    int minIndex = startIndex - maxDistance; //минимальное значение, дальше которого не будем искать

        //    //заполняем максимальными значениями на клетках текущей позиции
        //    for (int x = target.X; x < size + target.X; x++)
        //    {
        //        for (int y = target.Y; y < size + target.Y; y++)
        //        {
        //            if (x >= 0 && y >= 0 && x < mapSize && y < mapSize) {
        //                map[x][y] = startIndex;
        //            }
        //        }
        //    }
        //    //добавляем стартовые клетки поиска
        //    List<XYWeight> findCells = new List<XYWeight>();
        //    for (int x = target.X; x < size + target.X; x++)
        //    {
        //        findCells.Add(new XYWeight(x, target.Y, startIndex));
        //        if (size > 1)
        //            findCells.Add(new XYWeight(x, target.Y + size - 1, startIndex));
        //    }
        //    for (int y = target.Y + 1; y < size + target.Y - 1; y++)
        //    {
        //        findCells.Add(new XYWeight(target.X, y, startIndex));
        //        findCells.Add(new XYWeight(target.X + size - 1, y, startIndex));
        //    }

        //    while (findCells.Count > 0)
        //    {
        //        int x = findCells[0].x;
        //        int y = findCells[0].y;
        //        int w = findCells[0].weight;

        //        for (int jj = 0; jj < 4; jj++)
        //        {
        //            int nx = x;
        //            int ny = y;
        //            if (jj == 0) nx--;
        //            if (jj == 1) ny--;
        //            if (jj == 2) nx++;
        //            if (jj == 3) ny++;

        //            if (nx >= 0 && nx < mapSize && ny >= 0 && ny < mapSize)
        //            {
        //                if (map[nx][ny] == 0)
        //                {
        //                    map[nx][ny] = w - 1;
        //                    int id = cellWithIdAny[nx][ny];
        //                    if (id >= 0)
        //                    {
        //                        //check builder
        //                        if (basicEntityIdGroups[EntityType.BuilderUnit].members.Contains(id))
        //                        {
        //                            list.Add(id);
        //                            if (list.Count >= builderCount)
        //                                break;
        //                        }
        //                    }
        //                    else
        //                    {
        //                        //add findCell
        //                        if (w > minIndex)
        //                            findCells.Add(new XYWeight(nx, ny, w - 1));
        //                    }
        //                }
        //                //можем не проверять уже занятые клетки, так как у нас волны распространяются по очереди 1-2-3-4 и т.д.

        //            }
        //        }
        //        findCells.RemoveAt(0);

        //        if (list.Count >= builderCount)
        //            break;
        //    }
        //    return list;
        //}

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
            int sightRange = properties[entityType].SightRange;
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
            int sightRange = properties[entityType].SightRange;
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
            if (x >= 0 && x < mapSize && y >=0 && y < mapSize)
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
            } else
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
                    targetD = 19 - tx + bx ;//19-15
                }
                if (xRight)
                {
                    targetD = 14  + random.Next(2); //14-15
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

            if ( targetD < 0 || targetD >= 20)
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
            }else
            {
                if (_playerView.CurrentTick < 500)
                    return new Vec2Int(73, 7);
                else if (_playerView.CurrentTick < 750)
                    return new Vec2Int(73, 73);
                else return new Vec2Int(7, 73);

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
            if (!IsFreeCellsRange(sx, sy, (horizontal)? sx : (sx + size - 1), (horizontal) ? (sy + size - 1):sy))
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
                    } else
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

        //void TrySelectFreeBuilderForBuild(EntityType buildingType)
        //{
        //    int buildingSize = properties[buildingType].Size;
        //    foreach(var id in basicEntityIdGroups[EntityType.BuilderUnit].members)
        //    {
        //        Vec2Int pos = entityMemories[id].myEntity.Position;
        //        bool posFinded = false;

        //        //left 
        //        int x = pos.X - buildingSize;
        //        int y = pos.Y;
        //        if (TryFindSpawnPlace(ref x, ref y, buildingSize, false))
        //        {
        //            posFinded = true;
        //        }
        //        else
        //        {
        //            //down
        //            x = pos.X;
        //            y = pos.Y - buildingSize;
        //            if (TryFindSpawnPlace(ref x, ref y, buildingSize, true))
        //            {
        //                posFinded = true;
        //            }
        //            else
        //            {
        //                //right
        //                x = pos.X+1;
        //                y = pos.Y;
        //                if (TryFindSpawnPlace(ref x, ref y, buildingSize, false))
        //                {
        //                    posFinded = true;
        //                }
        //                else
        //                {
        //                    //up
        //                    x = pos.X;
        //                    y = pos.Y + 1;
        //                    if (TryFindSpawnPlace(ref x, ref y, buildingSize, true))
        //                    {
        //                        posFinded = true;
        //                    }
        //                }
        //            }
        //        }
        //        if (posFinded)
        //        {
        //            entityMemories[id].SetGroup(groupHouseBuilders);                    
        //            entityMemories[id].SetTargetPos(new Vec2Int(x, y));
        //            entityMemories[id].SetMovePos(entityMemories[id].myEntity.Position);
        //            entityMemories[id].SetTargetEntityType(EntityType.House);
        //            break;
        //        }
        //    }
        //}
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
            foreach(var p in previousEntityCount)
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

        Color colorWhite = new Color(1, 1, 1, 1);
        Color colorMagenta = new Color(1, 0, 1, 1);
        Color colorRed = new Color(1, 0, 0, 1);
        Color colorBlack = new Color(0, 0, 0, 1);
        Color colorGreen = new Color(0, 1, 0, 1);
        Color colorBlue = new Color(0, 0, 1, 1);
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
                            if (resourceMemoryMap[x][y] > 0 && resourceMemoryMap[x][y] < currentTick)
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
                debugInterface.Send(new DebugCommand.Flush());
            }
        }

    }

    enum EntityOrders { spawnUnit, buildNow, buildGo, repairGo, tryRetreat, canRetreat, attack, attackAndMove, collect, move, cancelAll, none}
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
        public void OrderTryRetreat()
        {
            order = EntityOrders.tryRetreat;
            optimized = false;
        }
        public void OrderCanRetreat(Vec2Int moveP, bool breakThrough, bool findClosestPosition, bool opt)
        {
            order = EntityOrders.canRetreat;
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
               
        public void SavePrevState ()
        {
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

    class Group{

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
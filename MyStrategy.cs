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


        List<int> needRepairEntityIdList = new List<int>();
        bool hasInactiveHouse = false;

        PlayerView _playerView;
        public IDictionary<EntityType, EntityProperties> properties;

        System.Random random = new System.Random();

        int[][] cellWithIdOnlyBuilding;
        int[][] cellWithIdAny;
        int[][] onceVisibleMap;
        bool[][] currentVisibleMap;
        int[][] resourceMemoryMap;
        int[][] resourcePotentialField;
        const int RPFmyBuildingWeight = -4;
        const int RPFenemyEntityWeight = -3;
        const int RPFdangerCellWeight = -2;
        const int RPFdeniedBuilderWeight = -1;

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

        int distToFindResThenSpawnBuilder = 30; // дистанция поиска пути при создании строителя иначе просто справа вверху создается

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
            WantCreateHouses, 
            WantExtractResources, WantRetreatBuilders,
            WantTurretAttacks, WantAllWarriorsAttack };
        List<DesireType> desires = new List<DesireType>();
        List<DesireType> prevDesires = new List<DesireType>();
        
        enum PlanType {PlanCreateBuilders, PlanCreateRangers,
            PlanCreateHouses, 
            PlanExtractResources, PlanRetreatBuilders,
            PlanTurretAttacks, PlanAllWarriorsAttack }
        List<PlanType> plans = new List<PlanType>();
        List<PlanType> prevPlans = new List<PlanType>();
        
        enum IntentionType { IntentionCreateBuilder, IntentionStopCreatingBuilder, 
            IntentionCreateRanger, IntentionStopCreatingRanger,
            IntentionCreateHouseStart, IntentionCreateHouseContionue, IntentionRepairBuilding,
            IntentionExtractResources, IntentionFindResources, IntentionRetreatBuilders, IntentionMyBuiAttackEnemyBui,
            IntentionTurretAttacks,
            IntentionAllWarriorsAttack
        }
        class Intention
        {
            public IntentionType intentionType;
            public int targetId;
            public Group targetGroup;
            public Vec2Int position;
            public EntityType entityType;

            public Intention(IntentionType type, Vec2Int pos, EntityType _entityType)
            {
                intentionType = type;
                targetId = -1;
                targetGroup = new Group();
                position = pos;
                entityType = _entityType;
            }
            public Intention(IntentionType type, int _targetId)
            {
                intentionType = type;
                targetId = _targetId;
                targetGroup = new Group();
                position = new Vec2Int();
                entityType = EntityType.Resource;
            }
            public Intention(IntentionType type, Group _targetGroup)
            {
                intentionType = type;
                targetId = -1;
                targetGroup = _targetGroup;
                position = new Vec2Int();
                entityType = EntityType.Resource;
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
            #endregion

            debugLines.Clear();

            GenerateDesires(); // Желания - Что я хочу сделать?       
            ConvertDesiresToPlans(); // Планы - Какие из желаний я могу сейчас сделать?

            PrepareBeforeCreateIntentions(); //Подготовительные меропрития перед созданиями намерений
            ConvertPlansToIntentions(); // Намерения - Как и кем я буду выполнять планы?
            CorrectCrossIntentions();// Проверяем взаимоискулючающие и противоречащие намерения. Оставляем только нужные.

            ConvertIntentionsToActions(); // Приказы - Кто будет выполнять намерения?
            //приказы превращаются в конкретные action для entities

            //old logics
            // FindBuildPriorities();
            //CheckEntitiesNeedRepair();
            //CheckEntitiesGroup();

            //var actions = GenerateActions();

            //save previous entity state
            //SaveEntitiesMemory();

            return new Action(actions);
        }

        Dictionary<int, EntityAction> GenerateActions()
        {
            var actions = new Dictionary<int, Model.EntityAction>();

            EntityType entityType;
            EntityProperties property;

            //////================= BUILDER BASE ================ actions
            ////entityType = EntityType.BuilderBase;
            ////property = properties[entityType];
            ////foreach (var id in basicEntityIdGroups[entityType].members)
            ////{
            ////    if (buildEntityPriority[EntityType.RangedUnit] <= buildEntityPriority[EntityType.BuilderUnit]
            ////        && buildEntityPriority[EntityType.BuilderUnit] > 0)
            ////    {
            ////        BuildAction buildAction = new BuildAction();

            ////        Vec2Int target = FindSpawnPosition(entityMemories[id].myEntity.Position.X, entityMemories[id].myEntity.Position.Y, false);

            ////        buildAction.EntityType = property.Build.Value.Options[0];
            ////        buildAction.Position = target;

            ////        actions.Add(id, new EntityAction(null, buildAction, null, null));
            ////    } else
            ////    {
            ////        actions.Add(id, new EntityAction(null, null, null, null));
            ////    }
            ////}

            //================= RANGER BASE ============ actions
            entityType = EntityType.RangedBase;
            property = properties[entityType];
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                if (buildEntityPriority[EntityType.RangedUnit] > buildEntityPriority[EntityType.BuilderUnit]
                    && buildEntityPriority[EntityType.RangedUnit] > 0)
                {
                    Vec2Int target = FindSpawnPosition(entityMemories[id].myEntity.Position.X, entityMemories[id].myEntity.Position.Y, true);

                    BuildAction buildAction = new BuildAction();
                    buildAction.EntityType = property.Build.Value.Options[0];
                    buildAction.Position = target;

                    actions.Add(id, new EntityAction(null, buildAction, null, null));
                }
                else
                {
                    actions.Add(id, new EntityAction(null, null, null, null));
                }
            }

            //================= MELEE BASE ============ actions
            entityType = EntityType.MeleeBase;
            property = properties[entityType];
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                if (myResources > 600)
                {
                    Vec2Int target = FindSpawnPosition(entityMemories[id].myEntity.Position.X, entityMemories[id].myEntity.Position.Y, true);

                    BuildAction buildAction = new BuildAction();
                    buildAction.EntityType = property.Build.Value.Options[0];
                    buildAction.Position = target;

                    actions.Add(id, new EntityAction(null, buildAction, null, null));
                }
                else
                {
                    actions.Add(id, new EntityAction(null, null, null, null));
                }
            }

            //================== basic BUILDER UNIT actions ===================
            entityType = EntityType.BuilderUnit;
            property = properties[entityType];
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                MoveAction moveAction = new MoveAction();
                moveAction.BreakThrough = true;
                moveAction.FindClosestPosition = true;
                moveAction.Target = new Vec2Int(_playerView.MapSize - 1, _playerView.MapSize - 1);

                AttackAction attackAction = new AttackAction();
                attackAction.AutoAttack = new AutoAttack(property.SightRange, new EntityType[] { EntityType.Resource });

                actions.Add(id, new EntityAction(moveAction, null, attackAction, null));
            }

            //=================== HOUSE builder unit ========================
            for (var i=0; i < groupHouseBuilders.members.Count; )
            {
                int id = groupHouseBuilders.members[i];
                bool removed = false;

                MoveAction moveAction = new MoveAction();
                moveAction.BreakThrough = true;
                moveAction.FindClosestPosition = true;
                moveAction.Target = entityMemories[id].movePos;
                                
                //build

                if (IsFreeCellsRange(
                    entityMemories[id].targetPos.X,
                    entityMemories[id].targetPos.Y,
                    entityMemories[id].targetPos.X + properties[entityMemories[id].targetEntityType].Size - 1,
                    entityMemories[id].targetPos.Y + properties[entityMemories[id].targetEntityType].Size - 1
                    ))
                {
                    BuildAction buildAction = new BuildAction(
                        entityMemories[id].targetEntityType,
                        entityMemories[id].targetPos
                        );
                    actions.Add(id, new EntityAction(moveAction, buildAction, null, null));
                } else
                {
                    //can't build
                    entityMemories[id].ResetTarget();
                    entityMemories[id].SetGroup(basicEntityIdGroups[EntityType.BuilderUnit]);
                    removed = true;
                    
                }
                
                if (!removed)
                    i++;
            }

            //=================== REPAIR builder unit ========================
            for (var i = 0; i < groupRepairBuilders.members.Count;)
            {
                int id = groupRepairBuilders.members[i];
                bool removed = false;
                
                int targetId = entityMemories[id].targetId;

                if (targetId >= 0)
                {
                    if (entityMemories.ContainsKey(targetId))
                    {
                        if (entityMemories[targetId].myEntity.Health == properties[entityMemories[targetId].myEntity.EntityType].MaxHealth)
                        {
                            entityMemories[id].ResetTarget();
                            entityMemories[id].SetGroup(basicEntityIdGroups[EntityType.BuilderUnit]);
                            removed = true;
                        }
                        else
                        {
                            //repair

                            MoveAction moveAction = new MoveAction();
                            moveAction.BreakThrough = false;
                            moveAction.FindClosestPosition = true;
                            moveAction.Target = entityMemories[targetId].myEntity.Position;

                            RepairAction repairAction = new RepairAction(entityMemories[id].targetId);
                            actions.Add(id, new EntityAction(moveAction, null, null, repairAction));
                        }
                    }
                    else
                    {
                        //target die
                        entityMemories[id].ResetTarget();
                        entityMemories[id].SetGroup(basicEntityIdGroups[EntityType.BuilderUnit]);
                        removed = true;
                    }
                }

                if (!removed)
                    i++;
            }

            //ranged UNIT actions
            entityType = EntityType.RangedUnit;
            property = properties[entityType];
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                MoveAction moveAction = new MoveAction();
                moveAction.BreakThrough = true;
                moveAction.FindClosestPosition = true;
                moveAction.Target = FindNearestEnemy(entityMemories[id].myEntity.Position.X, entityMemories[id].myEntity.Position.Y);

                AttackAction attackAction = new AttackAction();
                attackAction.AutoAttack = new AutoAttack(property.SightRange, new EntityType[] { });

                actions.Add(id, new EntityAction(moveAction, null, attackAction, null));
            }

            // =============== MELEE UNIT actions
            entityType = EntityType.MeleeUnit;
            property = properties[entityType];
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                MoveAction moveAction = new MoveAction();
                moveAction.BreakThrough = true;
                moveAction.FindClosestPosition = true;
                moveAction.Target = FindNearestEnemy(entityMemories[id].myEntity.Position.X, entityMemories[id].myEntity.Position.Y);


                AttackAction attackAction = new AttackAction();
                attackAction.AutoAttack = new AutoAttack(property.SightRange, new EntityType[] { });

                actions.Add(id, new EntityAction(moveAction, null, attackAction, null));
            }

            //=========== TURRET =========== actions
            entityType = EntityType.Turret;
            property = properties[entityType];
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                AttackAction attackAction = new AttackAction();
                attackAction.AutoAttack = new AutoAttack(property.SightRange, new EntityType[] { });

                actions.Add(id, new EntityAction(null, null, attackAction, null));
            }

            return actions;
        }
        void Prepare()
        {
            //init arrays
            cellWithIdAny = new int[mapSize][];
            cellWithIdOnlyBuilding = new int[mapSize][];
            onceVisibleMap = new int[mapSize][];
            currentVisibleMap = new bool[mapSize][];
            resourceMemoryMap = new int[mapSize][];
            enemyDangerCells = new EnemyDangerCell[mapSize][];
            resourcePotentialField = new int[mapSize][];

            for (var i = 0; i < mapSize; i++)
            {
                cellWithIdAny[i] = new int[mapSize];
                cellWithIdOnlyBuilding[i] = new int[mapSize];
                onceVisibleMap[i] = new int[mapSize];
                currentVisibleMap[i] = new bool[mapSize];
                resourceMemoryMap[i] = new int[mapSize];
                enemyDangerCells[i] = new EnemyDangerCell[mapSize];
                resourcePotentialField[i] = new int[mapSize];
                // auto zeroing all when created
            }

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
            //for (int x = 0; x < mapSize; x++)
            //{
            //    for (int y = 0; y < mapSize; y++)
            //    {
            //        // cellWithIdAny - zeroing before use
            //        // cellWithIdOnlyBuilding - zeroing before use
            //        onceVisibleMap[x][y] = 0; //update once
            //    }
            //}

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
            hasInactiveHouse = false;

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
                    if (e.Active == false)
                    {
                        hasInactiveHouse = true;
                    }
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
                                if (intentions[i].intentionType == IntentionType.IntentionCreateHouseStart)
                                {
                                    if (intentions[i].position.X == em.myEntity.Position.X && intentions[i].position.Y == em.myEntity.Position.Y)
                                    {
                                        intentions[i].targetId = em.myEntity.Id;
                                        break;
                                    }
                                }
                            }
                        }
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
                            if (dCell.meleesAim + dCell.meleesWarning + dCell.rangersAim + dCell.rangersWarning + dCell.turretsAim > 0)
                            {
                                canContinueField = false;
                                resourcePotentialField[nx][ny] = RPFdangerCellWeight;
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

        void GenerateDesires()
        {
            prevDesires.Clear();
            prevDesires = desires;
            desires = new List<DesireType>();

            #region Хочу строить дома
            int[] popMax = new int[] {  15, 30, 55, 70, 100, 1000 };
            int[] popRange = new int[] { 0, 4, 8, 10, 15, 20 };
            for(int i = 0; i < popMax.Length; i++)
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
            if (countEnemiesOnMyTerritory > 0)
            {
                if (currentMyEntityCount[EntityType.MeleeUnit] + currentMyEntityCount[EntityType.RangedUnit] <= countEnemiesOnMyTerritory + populationMax / 5)
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
                } else
                {
                    if (currentMyEntityCount[EntityType.BuilderUnit] < currentMyEntityCount[EntityType.RangedUnit] * 2)
                        desires.Add(DesireType.WantCreateBuilders);
                    else 
                        desires.Add(DesireType.WantCreateRangers);
                }
            }
            #endregion

            desires.Add(DesireType.WantRetreatBuilders);
            desires.Add(DesireType.WantExtractResources);

            desires.Add(DesireType.WantTurretAttacks);
            desires.Add(DesireType.WantAllWarriorsAttack);


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
                            if (howMuchResourcesIHaveNextTurn >= newCost)
                            {
                                // ограничение на одновременное строительство
                                int count = 0;
                                foreach(var ni in intentions)
                                {
                                    if (ni.intentionType == IntentionType.IntentionCreateHouseStart || ni.intentionType == IntentionType.IntentionCreateHouseContionue) 
                                        count++;
                                }
                                if ((populationMax <= 30 && count == 0) || (populationMax <= 60 && count <= 1) || (count <= 2))
                                {
                                    plans.Add(PlanType.PlanCreateHouses);
                                }
                            }
                        }
                        break;
                    #endregion
                    case DesireType.WantRetreatBuilders:
                        #region хочу чтобы строители сбегали от врагов
                        plans.Add(PlanType.PlanRetreatBuilders);
                        break;
                    #endregion
                    case DesireType.WantExtractResources:
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
                        int k = 5;//unknown type
                        break;
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
                        Vec2Int pos = FindPositionToBuildHouse();
                        if (pos.X >= 0) {
                            Intention intention = new Intention(IntentionType.IntentionCreateHouseStart, pos, EntityType.House);
                            List<int> buildersId = FindFreeNearestBuilders(
                                pos,
                                properties[EntityType.House].Size,
                                builderCountForStartBuilding,
                                (int)(properties[EntityType.House].MaxHealth * startBuildingFindDistanceFromHealth));
                            if (buildersId.Count > 0)
                            {
                                foreach (var id in buildersId)
                                {
                                    entityMemories[id].SetGroup(intention.targetGroup);
                                }
                                intentions.Add(intention);
                            } else
                            {
                                int j = 0;//yне должно такого быть
                            }
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
                        int k = 5;// unknown type
                        break;
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
                        break;
                    #endregion
                    case IntentionType.IntentionCreateRanger:
                        #region // cancel Ranger base build
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
                                intentions.Add(new Intention(IntentionType.IntentionStopCreatingRanger, prevIntentions[i].targetId));
                        }
                        break;
                    #endregion
                    case IntentionType.IntentionCreateHouseStart:
                        #region Создаем намерение на ремонт построенного или отменяем строительство
                        if (prevIntentions[i].targetId >= 0)
                        {
                            // удачное строительство, создавем намерение на ремонт
                            Intention intention = new Intention(IntentionType.IntentionRepairBuilding, prevIntentions[i].targetId);
                            intention.targetGroup = prevIntentions[i].targetGroup;
                            intentions.Add(intention);
                        }
                        else
                        {
                            // неудачное строительство, распускаем группу
                            while (prevIntentions[i].targetGroup.members.Count > 0)
                            {
                                int id = prevIntentions[i].targetGroup.members[0];
                                entityMemories[id].SetGroup(basicEntityIdGroups[entityMemories[id].myEntity.EntityType]);
                            }
                        }
                        break;
                    #endregion
                    case IntentionType.IntentionRepairBuilding:
                        #region продолжаем ремонтировать или отменяем задание
                        {
                            bool removed = false;
                            int targetId = prevIntentions[i].targetId;

                            if (entityMemories.ContainsKey(prevIntentions[i].targetId))
                            {
                                if (entityMemories[targetId].myEntity.Health == properties[entityMemories[targetId].myEntity.EntityType].MaxHealth)
                                {
                                    removed = true;
                                }
                            }
                            else
                            {
                                //target die
                                removed = true;
                            }
                            
                            if (removed)
                            {
                                while (prevIntentions[i].targetGroup.members.Count > 0)
                                {
                                    int id = prevIntentions[i].targetGroup.members[0];
                                    entityMemories[id].SetGroup(basicEntityIdGroups[entityMemories[id].myEntity.EntityType]);
                                }
                            }
                            else
                            {
                                intentions.Add(prevIntentions[i]);
                            }
                        }
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
        void ConvertIntentionsToActions()
        {
            actions.Clear();

            foreach (var ni in intentions)
            {
                switch (ni.intentionType)
                {
                    case IntentionType.IntentionCreateBuilder:
                        ActCreateUnit(ni.targetId, false);
                        break;
                    case IntentionType.IntentionStopCreatingBuilder:
                        ActCancelAll(ni.targetId);
                        break;
                    case IntentionType.IntentionCreateRanger:
                        ActCreateUnit(ni.targetId, true);
                        break;
                    case IntentionType.IntentionStopCreatingRanger:
                        ActCancelAll(ni.targetId);
                        break;
                    case IntentionType.IntentionExtractResources:
                        {
                            int dist = properties[EntityType.BuilderUnit].SightRange;
                            foreach (var id in ni.targetGroup.members)
                            {
                                ActExtractResources(id, dist);
                            }
                        }
                        break;
                    case IntentionType.IntentionCreateHouseStart:
                        foreach (int id in ni.targetGroup.members)
                        {
                            ActStartCreateBuilding(id, ni.position, EntityType.House);
                        }
                        break;
                    case IntentionType.IntentionRepairBuilding:
                        foreach (int id in ni.targetGroup.members)
                        {
                            ActRepairBuilding(id, ni.targetId);
                        }
                        break;
                    case IntentionType.IntentionTurretAttacks:
                        foreach (int id in ni.targetGroup.members)
                        {
                            ActTurretAttack(id);
                        }
                        break;
                    case IntentionType.IntentionAllWarriorsAttack:
                        foreach (int id in ni.targetGroup.members)
                        {
                            ActAttackNearbyEnemy(id, new EntityType[] { });
                        }
                        break;
                    case IntentionType.IntentionRetreatBuilders:
                        foreach (int id in ni.targetGroup.members)
                        {
                            ActRetreatFromEnemy(id);
                        }
                        break;
                    case IntentionType.IntentionMyBuiAttackEnemyBui:
                        foreach (int id in ni.targetGroup.members)
                        {
                            ActAttackNearbyEnemy(id, new EntityType[] { EntityType.BuilderUnit});
                        }
                        break;
                }
            }
        }

        void ActCreateUnit(int baseId, bool agressive)
        {
            BuildAction buildAction = new BuildAction();
            Vec2Int target;
            if (agressive)
            {
                target = FindSpawnPosition(entityMemories[baseId].myEntity.Position.X, entityMemories[baseId].myEntity.Position.Y, agressive);
            } else
            {
                //make builder
                var a = FindNearestToBaseResourceReturnSpawnPlace(entityMemories[baseId].myEntity.Position.X, entityMemories[baseId].myEntity.Position.Y, distToFindResThenSpawnBuilder);
                target = new Vec2Int(a.startX, a.startY);
            }

            buildAction.EntityType = properties[entityMemories[baseId].myEntity.EntityType].Build.Value.Options[0];
            buildAction.Position = target;

            actions.Add(baseId, new EntityAction(null, buildAction, null, null));
        }
        void ActCancelAll(int id)
        {
            actions.Add(id, new EntityAction(null, null, null, null));
        }
        void ActExtractResources(int id, int distance)
        {
            MoveAction moveAction = new MoveAction();
            moveAction.BreakThrough = true;
            moveAction.FindClosestPosition = true;
            moveAction.Target = new Vec2Int(_playerView.MapSize - 1, _playerView.MapSize - 1);

            AttackAction attackAction = new AttackAction();
            attackAction.AutoAttack = new AutoAttack(distance, new EntityType[] { EntityType.Resource });

            actions.Add(id, new EntityAction(moveAction, null, attackAction, null));
        }
        void ActStartCreateBuilding(int id, Vec2Int pos, EntityType type)
        {            
            MoveAction moveAction = new MoveAction();
            moveAction.BreakThrough = false;
            moveAction.FindClosestPosition = true;
            moveAction.Target = new Vec2Int( pos.X + properties[type].Size / 2, pos.Y + properties[type].Size);

            BuildAction buildAction = new BuildAction(type, pos);

            actions.Add(id, new EntityAction(moveAction, buildAction, null, null));
            
        }
        void ActRepairBuilding(int id, int targetId)
        {
            //repair
            MoveAction moveAction = new MoveAction();
            moveAction.BreakThrough = false;
            moveAction.FindClosestPosition = true;
            moveAction.Target = entityMemories[targetId].myEntity.Position;

            RepairAction repairAction = new RepairAction(targetId);
            actions.Add(id, new EntityAction(moveAction, null, null, repairAction));                                
        }
        void ActRetreatFromEnemy (int id)
        {
            int sx = entityMemories[id].myEntity.Position.X;
            int sy = entityMemories[id].myEntity.Position.Y;

            List<Vec2Int> targetsWarning = new List<Vec2Int>();
            List<Vec2Int> targetsSafe = new List<Vec2Int>();

            
            for (int k = 0; k < 4; k++)
            {
                int x = sx;
                int y = sy;
                if (k == 0) x--;
                if (k == 1) x++;
                if (k == 2) y--;
                if (k == 3) y++;

                if (x >= 0  && y >= 0 && x < mapSize && y < mapSize) // valid XY
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
                MoveAction moveAction = new MoveAction();
                moveAction.BreakThrough = false;
                moveAction.FindClosestPosition = true;
                moveAction.Target = targetsSafe[random.Next(targetsSafe.Count)];
                debugLines.Add(new DebugLine(sx + 0.5f, sy + 0.5f, moveAction.Target.X + 0.5f, moveAction.Target.Y + 0.5f, colorGreen, colorGreen));
                actions.Add(id, new EntityAction(moveAction, null, null, null));
                
            } else if(targetsWarning.Count > 0)
            {
                MoveAction moveAction = new MoveAction();
                moveAction.BreakThrough = false;
                moveAction.FindClosestPosition = true;
                moveAction.Target = targetsWarning[random.Next(targetsWarning.Count)];
                debugLines.Add(new DebugLine(sx + 0.5f, sy + 0.5f, moveAction.Target.X + 0.5f, moveAction.Target.Y + 0.5f, colorBlue, colorBlue));
                actions.Add(id, new EntityAction(moveAction, null, null, null));

            }
            else
            {
                AttackAction attackAction = new AttackAction();
                attackAction.AutoAttack = new AutoAttack(properties[entityMemories[id].myEntity.EntityType].SightRange, new EntityType[] { });
                debugLines.Add(new DebugLine(sx, sy, sx + 1, sy + 1, colorRed, colorRed));
                actions.Add(id, new EntityAction(null, null, attackAction, null));
            }

        }
        void ActTurretAttack(int id)
        {
            int range = properties[EntityType.Turret].SightRange;

            bool[] availableTargetsType = FindAvailableTargetType(
                entityMemories[id].myEntity.Position.X,
                entityMemories[id].myEntity.Position.Y,
                properties[EntityType.Turret].Size,
                range);

            AttackAction attackAction = new AttackAction();
            if (availableTargetsType[(int)EntityType.RangedUnit] == true)
                attackAction.AutoAttack = new AutoAttack(range, new EntityType[] { EntityType.RangedUnit });
            else if (availableTargetsType[(int)EntityType.MeleeUnit] == true)
                attackAction.AutoAttack = new AutoAttack(range, new EntityType[] { EntityType.RangedUnit, EntityType.MeleeUnit });
            else if (availableTargetsType[(int)EntityType.BuilderUnit] == true)
                attackAction.AutoAttack = new AutoAttack(range, new EntityType[] { EntityType.RangedUnit, EntityType.MeleeUnit, EntityType.BuilderUnit });
            else
                attackAction.AutoAttack = new AutoAttack(range, new EntityType[] { });

            actions.Add(id, new EntityAction(null, null, attackAction, null));
        }
        void ActAttackNearbyEnemy(int id, EntityType[] entityTypes)
        {
            MoveAction moveAction = new MoveAction();
            moveAction.BreakThrough = true;
            moveAction.FindClosestPosition = true;
            moveAction.Target = FindNearestEnemy(entityMemories[id].myEntity.Position.X, entityMemories[id].myEntity.Position.Y);

            AttackAction attackAction = new AttackAction();
            attackAction.AutoAttack = new AutoAttack(properties[entityMemories[id].myEntity.EntityType].SightRange, entityTypes);

            actions.Add(id, new EntityAction(moveAction, null, attackAction, null));
        }

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

        Vec2Int FindPositionToBuildHouse()
        {
            int buildingSize = properties[EntityType.House].Size;
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

        struct XYWeight
        {
            public int x;
            public int y;
            public int weight;
            public XYWeight(int _x, int _y, int _weight)
            {
                x = _x;
                y = _y;
                weight = _weight;
            }
        }
        List<int> FindFreeNearestBuilders(Vec2Int target, int size, int builderCount, int maxDistance)
        {
            List<int> list = new List<int>();

            if (builderCount > basicEntityIdGroups[EntityType.BuilderUnit].members.Count)
            {
                builderCount = basicEntityIdGroups[EntityType.BuilderUnit].members.Count;
            }

            if (builderCount == 0) 
                return list;

            int[][] map = new int[mapSize][];
            for (int i = 0; i < mapSize; i++)
            {
                map[i] = new int[mapSize];
            }

            int startIndex = mapSize * mapSize; //стартовое значение, которое будем уменьшать
            int minIndex = startIndex - maxDistance; //минимальное значение, дальше которого не будем искать

            //заполняем максимальными значениями на клетках текущей позиции
            for (int x = target.X; x < size + target.X; x++)
            {
                for (int y = target.Y; y < size + target.Y; y++)
                {
                    if (x >= 0 && y >= 0 && x < mapSize && y < mapSize) {
                        map[x][y] = startIndex;
                    }
                }
            }
            //добавляем стартовые клетки поиска
            List<XYWeight> findCells = new List<XYWeight>();
            for (int x = target.X; x < size + target.X; x++)
            {
                findCells.Add(new XYWeight(x, target.Y, startIndex));
                if (size > 1)
                    findCells.Add(new XYWeight(x, target.Y + size - 1, startIndex));
            }
            for (int y = target.Y + 1; y < size + target.Y - 1; y++)
            {
                findCells.Add(new XYWeight(target.X, y, startIndex));
                findCells.Add(new XYWeight(target.X + size - 1, y, startIndex));
            }

            while (findCells.Count > 0)
            {
                int x = findCells[0].x;
                int y = findCells[0].y;
                int w = findCells[0].weight;

                for (int jj = 0; jj < 4; jj++)
                {
                    int nx = x;
                    int ny = y;
                    if (jj == 0) nx--;
                    if (jj == 1) ny--;
                    if (jj == 2) nx++;
                    if (jj == 3) ny++;

                    if (nx >= 0 && nx < mapSize && ny >= 0 && ny < mapSize)
                    {
                        if (map[nx][ny] == 0)
                        {
                            map[nx][ny] = w - 1;
                            int id = cellWithIdAny[nx][ny];
                            if (id >= 0)
                            {
                                //check builder
                                if (basicEntityIdGroups[EntityType.BuilderUnit].members.Contains(id))
                                {
                                    list.Add(id);
                                    if (list.Count >= builderCount)
                                        break;
                                }
                            }
                            else
                            {
                                //add findCell
                                if (w > minIndex)
                                    findCells.Add(new XYWeight(nx, ny, w - 1));
                            }
                        }
                        //можем не проверять уже занятые клетки, так как у нас волны распространяются по очереди 1-2-3-4 и т.д.

                    }
                }
                findCells.RemoveAt(0);

                if (list.Count >= builderCount)
                    break;
            }
            return list;
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
        StartAndTargetPoint FindNearestToBaseResourceReturnSpawnPlace(int baseX, int baseY, int maxDistance)
        {
            int size = properties[EntityType.BuilderBase].Size;
            int startIndex = mapSize * mapSize; //стартовое значение, которое будем уменьшать
            int minIndex = startIndex - maxDistance; //минимальное значение, дальше которого не будем искать

            bool iFind = false;
            int resourceX = 0;
            int resourceY = 0;

            #region найди ближайший ресурс
            int[][] map = new int[mapSize][];
            for (int i = 0; i < mapSize; i++)
            {
                map[i] = new int[mapSize];
            }

            //заполняем максимальными значениями на клетках текущей позиции
            for (int x = baseX; x < size + baseX; x++)
            {
                for (int y = baseY; y < size + baseY; y++)
                {
                    if (x >= 0 && y >= 0 && x < mapSize && y < mapSize)
                    {
                        map[x][y] = startIndex;
                    }
                }
            }

            //добавляем стартовые клетки поиска
            List<XYWeight> findCells = new List<XYWeight>();
            for (int x = baseX; x < size + baseX; x++)
            {
                findCells.Add(new XYWeight(x, baseY, startIndex));
                if (size > 1)
                    findCells.Add(new XYWeight(x, baseY + size - 1, startIndex));
            }
            for (int y = baseY + 1; y < size + baseY - 1; y++)
            {
                findCells.Add(new XYWeight(baseX, y, startIndex));
                findCells.Add(new XYWeight(baseX + size - 1, y, startIndex));
            }

            while (findCells.Count > 0)
            {
                int x = findCells[0].x;
                int y = findCells[0].y;
                int w = findCells[0].weight;

                for (int jj = 0; jj < 4; jj++)// обследование четырех соседних клеток
                {
                    int nx = x;
                    int ny = y;
                    if (jj == 0) nx--;
                    if (jj == 1) ny--;
                    if (jj == 2) nx++;
                    if (jj == 3) ny++;

                    if (nx >= 0 && nx < mapSize && ny >= 0 && ny < mapSize)
                    {
                        if (map[nx][ny] == 0)
                        {
                            // это ресурс?
                            if (resourceMemoryMap[nx][ny] > 0)
                            {
                                resourceX = nx;
                                resourceY = ny;
                                map[nx][ny] = w - 1;
                                iFind = true;
                                break;
                            }
                            // ищем путь дальше или что-то мешает?                                                        
                            int id = cellWithIdAny[nx][ny];
                            if (id >= 0)
                            {
                                // что-то мешает, но это не ресурс
                            }
                            else
                            {
                                // ищем дальше
                                map[nx][ny] = w - 1;
                                if (w > minIndex)
                                    findCells.Add(new XYWeight(nx, ny, w - 1));
                            }
                        }
                        //можем не проверять уже занятые клетки, так как у нас волны распространяются по очереди 1-2-3-4 и т.д.
                    }
                }
                findCells.RemoveAt(0);

                if (iFind)
                    break;
            }
            #endregion

            int targetX = resourceX;
            int targetY = resourceY;

            if (iFind)
            {
                #region распутай путь до первой клетки и верни его
                int currentNum = map[targetX][targetY];
                int targetNum = startIndex - 1;

                while(currentNum < targetNum)
                {
                    int nextNum = currentNum + 1;
                    bool find = false;
                    if (targetX > 0)//can check left
                    {
                        if (map[targetX - 1][targetY] == nextNum)
                        {
                            targetX--;
                            find = true;
                        }   
                    } 
                    if (!find && targetX + 1 < mapSize)//can check right
                    {
                        if (map[targetX + 1][targetY] == nextNum)
                        {
                            targetX++;
                            find = true;
                        }
                    } 
                    if (!find && targetY + 1 < mapSize)//can check up
                    {
                        if (map[targetX][targetY+1] == nextNum)
                        {
                            find = true;
                            targetY++;
                        }
                    } 
                    if (!find && targetY > 0)
                    {
                        if (map[targetX][targetY - 1] == nextNum)
                        {
                            targetY--;
                            find = true;
                        }
                    }
                    if (find)
                    {
                        currentNum = map[targetX][targetY];
                    } else
                    {
                        break;//не должно быть такого
                    }
                }

                return new StartAndTargetPoint(targetX, targetY, resourceX, resourceY);
                #endregion
            }
            else
            {
                #region если не нашелся, то верни правую-верхнюю свободную
                //find nearest building cell
                int positionDindex = 9; //left upper

                //if needed select nearest free 
                for (int i = 0; i < 20; i++)
                {
                    int index = positionDindex + buildingPositionIter[i];
                    if (index < 0) index += 20;
                    if (index >= 20) index -= 20;

                    int kx = baseX + buildingPositionDX[index];
                    int ky = baseY + buildingPositionDY[index];

                    if (kx >= 0
                        && kx < _playerView.MapSize
                        && ky >= 0
                        && ky < _playerView.MapSize)
                    {
                        if (cellWithIdAny[kx][ky] < 0)
                        {
                            return new StartAndTargetPoint(kx, ky, kx, ky);
                        }
                    }
                }
                #endregion
            }
            return new StartAndTargetPoint(0, 0, 0, 0);
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
                return new Vec2Int(_playerView.MapSize / 2, _playerView.MapSize / 2);
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

        void TrySelectFreeBuilderForBuild(EntityType buildingType)
        {
            int buildingSize = properties[buildingType].Size;
            foreach(var id in basicEntityIdGroups[EntityType.BuilderUnit].members)
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
                        x = pos.X+1;
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
                    entityMemories[id].SetGroup(groupHouseBuilders);                    
                    entityMemories[id].SetTargetPos(new Vec2Int(x, y));
                    entityMemories[id].SetMovePos(entityMemories[id].myEntity.Position);
                    entityMemories[id].SetTargetEntityType(EntityType.House);
                    break;
                }
            }
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

        Color colorWhite = new Color(1, 1, 1, 1);
        Color colorMagenta = new Color(1, 0, 1, 1);
        Color colorRed = new Color(1, 0, 0, 1);
        Color colorBlack = new Color(0, 0, 0, 1);
        Color colorGreen = new Color(0, 1, 0, 1);
        Color colorBlue = new Color(0, 0, 1, 1);
        public void DebugUpdate(PlayerView playerView, DebugInterface debugInterface)
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
                bool drawResourcePotentialField = true;
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
                                debugInterface.Send(new DebugCommand.Add(new DebugData.PlacedText(position, "-", 0.5f, 14)));
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

    class EntityMemory
    {
        public Group group { get; private set; }
        public int prevHealth { get; private set; }
        public Vec2Int prevPosition { get; private set; }
        public int myId { get; private set; }
        public int targetId { get; private set; }
        public Vec2Int targetPos { get; private set; }
        public Vec2Int movePos { get; private set; }
        public EntityType targetEntityType { get; private set; }

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
            targetId = -1;
            targetPos = new Vec2Int(-1, -1);
            movePos = new Vec2Int(-1, -1);
        }
        public void Update(Entity entity)
        {
            checkedNow = true;
            myEntity = entity;
        }

        public void ResetTarget()
        {
            targetId = -1;
            targetPos = new Vec2Int(-1, -1);
            movePos = new Vec2Int(-1, -1);
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

        public void SetTargetId(int id)
        {
            targetId = id;
        }
        public void SetTargetPos(Vec2Int vec)
        {
            targetPos = vec;
        }
        public void SetMovePos(Vec2Int vec)
        {
            movePos = vec;
        }
        public void SetTargetEntityType(EntityType entityType)
        {
            targetEntityType = entityType;
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
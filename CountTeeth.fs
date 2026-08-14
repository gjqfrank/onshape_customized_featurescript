FeatureScript 3044;

import(path : "onshape/std/geometry.fs", version : "3044.0");

// ===========================================================================
// Count Teeth —— 识别带轮(pulley) / 链轮(sprocket) / 齿轮(gear) 的齿数
//
// 新方案(只需选一个 part):
//   1. 自动找回转轴: 找所有圆柱面, 聚类同心轴, 选包含最多圆柱面的轴
//      (回转体上有多个同心圆柱面: 内孔、轮毂、齿根圆等)
//   2. 几何中心: bbox 中心投影到轴上
//   3. 标识轴(黄线)和中心(黄点)
//   4. 全局径向采样: 遍历所有边, 按角度分360桶, 每桶取最大半径
//      → "角度-最大半径"曲线的周期性峰值 = 齿数
//      (不依赖边环, 不受减重孔/倒圆干扰, 因为齿顶半径永远最大)
// ===========================================================================

// 点到轴线的径向距离
function radialDistance(point is Vector, axisOrigin is Vector, axisDir is Vector) returns ValueWithUnits
{
    var d = point - axisOrigin;
    return norm(d - dot(d, axisDir) * axisDir);
}

// 点到轴线的垂直距离(用于判断两轴是否同心)
function pointToAxisDistance(point is Vector, axisOrigin is Vector, axisDir is Vector) returns ValueWithUnits
{
    var d = point - axisOrigin;
    return norm(cross(d, axisDir));
}

// (齿数由径向剖面的滞回阈值上升沿计数决定; 顶点仅用于红点可视化)

annotation { "Feature Type Name" : "Count Teeth", "Feature Type Description" : "Count teeth of a pulley or sprocket" }
export const countTeeth = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "Pulley or sprocket part", "Filter" : EntityType.BODY && BodyType.SOLID, "MaxNumberOfPicks" : 1 }
        definition.part is Query;

        annotation { "Name" : "Rename part using tooth count", "Default" : false }
        definition.rename is boolean;

        annotation { "Name" : "Name prefix", "Default" : "Pulley" }
        definition.namePrefix is string;
    }
    {
        // ---------- 1. 自动找回转轴 -----------------------------------------
        // 找所有面, 尝试 evAxis 找圆柱面, 收集时去重(方向平行+原点共线视为同一个)
        var allFaces = evaluateQuery(context, qOwnedByBody(definition.part, EntityType.FACE));

        var axes = []; // 去重后的轴列表
        var counts = []; // 每根轴出现的次数
        var tol = 0.01 * meter; // 1cm 同心容差

        for (var face in allFaces)
        {
            try silent
            {
                var ax = evAxis(context, { "axis" : face });
                // 检查是否已有近似轴
                var found = false;
                for (var k = 0; k < size(axes); k += 1)
                {
                    if (abs(dot(axes[k].direction, ax.direction)) > 0.999 &&
                        pointToAxisDistance(axes[k].origin, ax.origin, ax.direction) < tol)
                    {
                        counts[k] += 1;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    axes = append(axes, ax);
                    counts = append(counts, 1);
                }
            }
        }

        if (size(axes) == 0)
            throw "No cylindrical face found, cannot auto-detect rotation axis.";

        // 选出现次数最多的轴(回转轴上有最多同心圆柱面: 内孔、轮毂、齿根圆等)
        var bestCount = counts[0];
        var bestAxis = axes[0];
        for (var k = 1; k < size(axes); k += 1)
        {
            if (counts[k] > bestCount)
            {
                bestCount = counts[k];
                bestAxis = axes[k];
            }
        }

        var axisOrigin is Vector = bestAxis.origin;
        var axisDir is Vector = bestAxis.direction;

        // ---------- 2. 几何中心 ---------------------------------------------
        var bbox = evBox3d(context, { "topology" : definition.part });
        var bboxCenter = (bbox.minCorner + bbox.maxCorner) / 2;
        // 投影到轴上
        var centerOnAxis = axisOrigin + dot(bboxCenter - axisOrigin, axisDir) * axisDir;

        // ---------- 3. 标识轴和中心 -----------------------------------------
        // 黄色画轴(用线)和中心点
        try silent { debug(context, line(centerOnAxis, axisDir), DebugColor.YELLOW); }
        debug(context, centerOnAxis, DebugColor.YELLOW);

        // ---------- 4. 全局径向采样 -----------------------------------------
        var allEdges = evaluateQuery(context, qOwnedByBody(definition.part, EntityType.EDGE));

        if (size(allEdges) == 0)
            throw "No edges found, cannot sample.";

        var refU;
        if (abs(axisDir[0]) < 0.9)
            refU = normalize(cross(axisDir, vector(1, 0, 0)));
        else
            refU = normalize(cross(axisDir, vector(0, 1, 0)));
        var refV = cross(axisDir, refU);

        // 每边采样数: 总采样约1440点, 每边最少8点
        var samplesPerEdge = floor(1440 / size(allEdges));
        if (samplesPerEdge < 8)
            samplesPerEdge = 8;

        // 第一遍: 收集所有采样点, 找全局最大半径
        var allSampleData = [];
        var globalMaxR = 0 * meter;
        for (var edge in allEdges)
        {
            for (var i = 0; i < samplesPerEdge; i += 1)
            {
                var t = (i + 0.5) / samplesPerEdge;
                try silent
                {
                    var pt = evEdgeTangentLine(context, { "edge" : edge, "parameter" : t }).origin;
                    var r = radialDistance(pt, axisOrigin, axisDir);
                    var d = pt - axisOrigin;
                    var proj = d - dot(d, axisDir) * axisDir;
                    var angle = atan2(dot(proj, refV), dot(proj, refU));
                    if (angle < 0)
                        angle += 2 * PI;
                    allSampleData = append(allSampleData, { "angle" : angle, "radius" : r });
                    if (r > globalMaxR) globalMaxR = r;
                }
            }
        }

        if (size(allSampleData) < 4)
            throw "Sampling failed. Samples: " ~ toString(size(allSampleData)) ~ " / Edges: " ~ toString(size(allEdges));

        // ---------- 5. 径向剖面 + 滞回阈值计数(主方法) ---------------------
        // 对平顶齿(gear)和尖顶齿(sprocket/pulley)都鲁棒:
        //   - 按1°分360桶, 每桶取最大半径 → "角度-半径"剖面
        //     (外缘齿廓是闭合环覆盖整圈; 取max可自然压掉减重孔/内孔, 因为齿顶半径永远最大)
        //   - tipR=剖面最大值(齿顶), rootR=剖面最小值(齿根)
        //   - 滞回: 上沿阈值=tipR-30%幅值, 下沿阈值=rootR+30%幅值
        //     每齿: 升过上沿(计数1) → 降过下沿(复位). 平顶齿整段高于上沿只算1次, 不会算成2齿
        var NBUCKET = 360;
        var profile = [];
        var hasSample = [];
        for (var i = 0; i < NBUCKET; i += 1)
        {
            profile = append(profile, 0 * meter);
            hasSample = append(hasSample, false);
        }
        for (var sd in allSampleData)
        {
            var angleDeg = sd.angle / degree;
            if (angleDeg < 0)
                angleDeg += 360;
            var bin = floor(angleDeg / 360 * NBUCKET);
            if (bin >= NBUCKET)
                bin = NBUCKET - 1;
            if (bin < 0)
                bin = 0;
            if (sd.radius > profile[bin])
            {
                profile[bin] = sd.radius;
                hasSample[bin] = true;
            }
        }

        // 填补空桶(圆周上线性插值相邻已填桶), 保证剖面连续
        for (var i = 0; i < NBUCKET; i += 1)
        {
            if (!hasSample[i])
            {
                var prev = -1;
                var next = -1;
                for (var j = 1; j <= NBUCKET; j += 1)
                {
                    var pi = (i - j + NBUCKET) % NBUCKET;
                    if (hasSample[pi]) { prev = pi; break; }
                }
                for (var j = 1; j <= NBUCKET; j += 1)
                {
                    var ni = (i + j) % NBUCKET;
                    if (hasSample[ni]) { next = ni; break; }
                }
                if (prev >= 0 && next >= 0)
                {
                    var span = (next - prev + NBUCKET) % NBUCKET;
                    var dp = (i - prev + NBUCKET) % NBUCKET;
                    if (span == 0)
                        profile[i] = profile[prev];
                    else
                        profile[i] = profile[prev] + (profile[next] - profile[prev]) * (dp / span);
                }
                else if (prev >= 0)
                    profile[i] = profile[prev];
            }
        }

        var tipR = 0 * meter;
        var rootR = profile[0];
        for (var i = 0; i < NBUCKET; i += 1)
        {
            if (profile[i] > tipR) tipR = profile[i];
            if (profile[i] < rootR) rootR = profile[i];
        }

        var teeth = 0;
        var amplitude = tipR - rootR;
        if (amplitude > tipR * 0.005)
        {
            var upThresh = tipR - amplitude * 0.3;
            var downThresh = rootR + amplitude * 0.3;
            // 从一个低于下沿的桶(齿根)开始, 避免起始状态歧义
            var startBin = 0;
            var foundStart = false;
            for (var i = 0; i < NBUCKET; i += 1)
            {
                if (profile[i] < downThresh) { startBin = i; foundStart = true; break; }
            }
            if (foundStart)
            {
                var high = false;
                for (var k = 0; k < NBUCKET; k += 1)
                {
                    var i = (startBin + k) % NBUCKET;
                    if (!high && profile[i] > upThresh) { high = true; teeth += 1; }
                    else if (high && profile[i] < downThresh) { high = false; }
                }
            }
            else
            {
                teeth = 1;
            }
        }

        // ---------- 5b. 红点标出齿尖顶点(仅可视化, 不参与计数) -------------
        var allVertices = evaluateQuery(context, qOwnedByBody(definition.part, EntityType.VERTEX));
        var vertexMaxR = 0 * meter;
        var vertexPoints = [];
        for (var v in allVertices)
        {
            try silent
            {
                var pt = evVertexPoint(context, { "vertex" : v });
                var r = radialDistance(pt, axisOrigin, axisDir);
                vertexPoints = append(vertexPoints, { "point" : pt, "radius" : r });
                if (r > vertexMaxR) vertexMaxR = r;
            }
        }
        var tipVertexCount = 0;
        for (var vp in vertexPoints)
        {
            if (vp.radius > vertexMaxR * 0.98)
            {
                debug(context, vp.point, DebugColor.RED);
                tipVertexCount += 1;
            }
        }

        // ---------- 6. 输出 -------------------------------------------------
        var diagMsg = "Teeth: " ~ toString(teeth)
            ~ " | Tip R: " ~ toString(tipR)
            ~ " | Root R: " ~ toString(rootR)
            ~ " | Amplitude: " ~ toString(amplitude)
            ~ " | Tip vertices: " ~ toString(tipVertexCount)
            ~ " | VMaxR: " ~ toString(vertexMaxR)
            ~ " | GlobalMaxR: " ~ toString(globalMaxR)
            ~ " | Edges: " ~ toString(size(allEdges));

        reportFeatureInfo(context, id, diagMsg);
        println(diagMsg);

        if (definition.rename && teeth > 0)
            setProperty(context, { "entities" : definition.part, "propertyName" : PropertyType.NAME, "value" : definition.namePrefix ~ " (" ~ toString(teeth) ~ "T)" });
    });

from shapely.geometry import LineString

line = LineString([(1,1),(10,20),(20,40),(60,60)])

print("Length {}".format(line.length))

for dist in range(int(line.length)):
    print("{} = {}".format(dist,line.interpolate(dist)))






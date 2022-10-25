## Tests against ConcurrentDictionary 

The comparison is of course not direct - SkipListMap is a sorted data structure, while ConcurrentDictionary is a hash-map. 
However, it does produce some point of reference   

|                               Method |   Size |          Mean |        Error |       StdDev | Completed Work Items | Lock Contentions |     Gen 0 |     Gen 1 |    Gen 2 | Allocated |
|------------------------------------- |------- |--------------:|-------------:|-------------:|---------------------:|-----------------:|----------:|----------:|---------:|----------:|
|       InsertRemoveEnumerate_SkipList |    100 |     230.17 us |     3.976 us |     3.719 us |                    - |           1.0330 |    4.3945 |    0.9766 |   0.9766 |      4 KB |
| InsertRemoveEnumerate_ConcurrentDict |    100 |     222.77 us |     3.641 us |     3.041 us |                    - |           2.1787 |    8.7891 |    1.4648 |   0.9766 |      3 KB |
|              ParallelInsert_SkipList |    100 |      45.81 us |     0.902 us |     1.350 us |               6.9163 |           0.1871 |    6.1646 |         - |        - |     12 KB |
|        ParallelInsert_ConcurrentDict |    100 |      17.77 us |     0.245 us |     0.204 us |               4.8195 |           0.1235 |    7.6294 |         - |        - |     15 KB |
|       InsertRemoveEnumerate_SkipList |   1000 |     597.63 us |     2.550 us |     1.991 us |                    - |           1.7070 |   12.6953 |    2.9297 |   0.9766 |     38 KB |
| InsertRemoveEnumerate_ConcurrentDict |   1000 |     222.76 us |     3.030 us |     2.834 us |                    - |           0.4141 |   16.6016 |    0.9766 |   0.9766 |     23 KB |
|              ParallelInsert_SkipList |   1000 |     397.25 us |     7.888 us |    20.502 us |              17.3745 |           1.8223 |   42.9688 |         - |        - |     85 KB |
|        ParallelInsert_ConcurrentDict |   1000 |     178.89 us |     4.832 us |    14.246 us |               9.9595 |           8.7051 |   59.5703 |         - |        - |    117 KB |
|       InsertRemoveEnumerate_SkipList |  10000 |   7,639.49 us |   112.179 us |    99.444 us |                    - |          37.7891 |  117.1875 |   31.2500 |   7.8125 |    374 KB |
| InsertRemoveEnumerate_ConcurrentDict |  10000 |   1,169.89 us |    20.325 us |    18.018 us |                    - |           0.0566 |   89.8438 |   31.2500 |        - |    262 KB |
|              ParallelInsert_SkipList |  10000 |   3,877.07 us |    82.131 us |   240.875 us |              54.0000 |          25.8008 |  312.5000 |  132.8125 |        - |    799 KB |
|        ParallelInsert_ConcurrentDict |  10000 |   2,463.61 us |   176.675 us |   520.931 us |              56.0723 |         213.4258 |  509.7656 |  175.7813 |  62.5000 |  1,436 KB |
|       InsertRemoveEnumerate_SkipList | 100000 | 114,012.50 us | 1,534.664 us | 1,281.514 us |                    - |         971.0000 |  800.0000 |  200.0000 |        - |  3,732 KB |
| InsertRemoveEnumerate_ConcurrentDict | 100000 |  13,852.09 us |   220.672 us |   172.286 us |                    - |           0.6719 |  640.6250 |  218.7500 |        - |  2,599 KB |
|              ParallelInsert_SkipList | 100000 |  39,052.15 us |   724.195 us | 1,169.441 us |              26.0769 |         113.9231 | 2153.8462 |  692.3077 | 153.8462 |  7,830 KB |
|        ParallelInsert_ConcurrentDict | 100000 |  21,583.31 us |   631.299 us | 1,861.399 us |              46.9375 |         429.3750 | 4468.7500 | 1781.2500 | 656.2500 | 13,395 KB |

### Tests against Sorted data structures

Again, direct comparison is not fair, as only the ConcurrentSkipList cares about concurrency. 
However, even taking concurrency overhead into account, we can say that SkipList does not 
fair great against tree-based SortedDictionary. 

Why do we use it then? Two reasons - (1) no sorted collection is thread safe out of the box. While SkipList is
arguably the simplest data structures to implement fine-grained concurrency control. Making a thread safe 
tree would be better, but it's a more complex tax. (2) The main goal was to outperform a SortedList in scaling.
This was indeed achieved - even at a modest 8k elements added in random order, 
SortedList becomes the slowest sorted collection.   


|                      Method |   Size |             Mean |         Error |        StdDev |     Gen 0 |    Gen 1 |    Gen 2 |    Allocated |
|---------------------------- |------- |-----------------:|--------------:|--------------:|----------:|---------:|---------:|-------------:|
| InsertSequential_SortedDict |    500 |        38.484 us |     0.5185 us |     0.4850 us |   13.4277 |        - |        - |     28,112 B |
| InsertSequential_SortedList |    500 |         8.659 us |     0.1326 us |     0.1176 us |    8.0109 |        - |        - |     16,768 B |
|   InsertSequential_SkipList |    500 |       128.910 us |     0.6456 us |     0.5391 us |   19.5313 |        - |        - |     41,067 B |
|     InsertRandom_SortedDict |    500 |        59.726 us |     0.5436 us |     0.4540 us |   13.4277 |        - |        - |     28,112 B |
|     InsertRandom_SortedList |    500 |        65.591 us |     0.1173 us |     0.0916 us |    7.9346 |        - |        - |     16,768 B |
|       InsertRandom_SkipList |    500 |       161.432 us |     0.3934 us |     0.3487 us |   19.5313 |        - |        - |     41,045 B |
|          Enumerate_SkipList |    500 |         1.466 us |     0.0096 us |     0.0085 us |         - |        - |        - |            - |
|              Enumerate_Dict |    500 |         5.525 us |     0.0076 us |     0.0059 us |    0.0916 |        - |        - |        200 B |
|              Enumerate_List |    500 |         1.943 us |     0.0011 us |     0.0009 us |    0.0267 |        - |        - |         56 B |
| InsertSequential_SortedDict |   2000 |       179.370 us |     1.4348 us |     1.2719 us |   52.9785 |   0.7324 |        - |    112,112 B |
| InsertSequential_SortedList |   2000 |        37.686 us |     0.0457 us |     0.0357 us |   31.4941 |        - |        - |     66,016 B |
|   InsertSequential_SkipList |   2000 |       566.659 us |     1.8510 us |     1.5457 us |   71.2891 |   6.8359 |        - |    161,239 B |
|     InsertRandom_SortedDict |   2000 |       275.506 us |     2.1815 us |     1.8217 us |   52.2461 |   1.4648 |        - |    112,112 B |
|     InsertRandom_SortedList |   2000 |       449.806 us |     2.9693 us |     2.7775 us |   31.2500 |        - |        - |     66,016 B |
|       InsertRandom_SkipList |   2000 |       737.553 us |     3.5675 us |     3.3370 us |   67.3828 |  15.6250 |        - |    161,132 B |
|          Enumerate_SkipList |   2000 |         6.151 us |     0.1190 us |     0.1169 us |         - |        - |        - |            - |
|              Enumerate_Dict |   2000 |        23.067 us |     0.2268 us |     0.2122 us |    0.0916 |        - |        - |        232 B |
|              Enumerate_List |   2000 |         7.729 us |     0.0307 us |     0.0257 us |    0.0153 |        - |        - |         56 B |
| InsertSequential_SortedDict |   8000 |       860.864 us |    11.0324 us |    10.3197 us |  211.9141 | 105.4688 |        - |    448,113 B |
| InsertSequential_SortedList |   8000 |       164.212 us |     0.3130 us |     0.2444 us |  124.7559 |        - |        - |    262,720 B |
|   InsertSequential_SkipList |   8000 |     2,518.910 us |    11.1508 us |     8.7058 us |  183.5938 |  74.2188 |        - |    640,524 B |
|     InsertRandom_SortedDict |   8000 |     1,326.730 us |     9.9810 us |     9.3363 us |  191.4063 |  82.0313 |        - |    448,114 B |
|     InsertRandom_SortedList |   8000 |     4,656.176 us |     7.3514 us |     6.8765 us |  117.1875 |        - |        - |    262,726 B |
|       InsertRandom_SkipList |   8000 |     3,544.607 us |     9.2235 us |     8.6276 us |  210.9375 |  82.0313 |        - |    641,236 B |
|          Enumerate_SkipList |   8000 |        23.749 us |     0.0081 us |     0.0067 us |         - |        - |        - |            - |
|              Enumerate_Dict |   8000 |        91.282 us |     0.0545 us |     0.0455 us |    0.1221 |        - |        - |        264 B |
|              Enumerate_List |   8000 |        30.869 us |     0.1093 us |     0.0854 us |         - |        - |        - |         56 B |
| InsertSequential_SortedDict |  32000 |     3,927.358 us |    28.6258 us |    23.9038 us |  570.3125 | 281.2500 |        - |  1,792,118 B |
| InsertSequential_SortedList |  32000 |       762.060 us |     6.0013 us |     5.0114 us |  272.4609 | 159.1797 | 150.3906 |  1,050,245 B |
|   InsertSequential_SkipList |  32000 |    11,420.623 us |   208.1636 us |   194.7164 us |  609.3750 | 265.6250 |        - |  2,561,698 B |
|     InsertRandom_SortedDict |  32000 |     6,413.499 us |    20.6356 us |    16.1109 us |  570.3125 | 281.2500 |        - |  1,792,118 B |
|     InsertRandom_SortedList |  32000 |    81,507.843 us |   158.7615 us |   132.5730 us |  142.8571 | 142.8571 | 142.8571 |  1,049,543 B |
|       InsertRandom_SkipList |  32000 |    17,968.612 us |   172.3580 us |   152.7908 us |  812.5000 | 312.5000 |  31.2500 |  2,561,316 B |
|          Enumerate_SkipList |  32000 |        96.148 us |     0.0634 us |     0.0562 us |         - |        - |        - |            - |
|              Enumerate_Dict |  32000 |       365.572 us |     0.2142 us |     0.1788 us |         - |        - |        - |        296 B |
|              Enumerate_List |  32000 |       123.222 us |     0.0598 us |     0.0499 us |         - |        - |        - |         56 B |
| InsertSequential_SortedDict | 128000 |    20,543.169 us |    85.5654 us |    75.8515 us | 2312.5000 | 812.5000 | 156.2500 |  7,168,674 B |
| InsertSequential_SortedList | 128000 |     3,238.643 us |    13.4535 us |    12.5844 us |  285.1563 | 183.5938 | 167.9688 |  4,196,104 B |
|   InsertSequential_SkipList | 128000 |    52,788.345 us |   699.8392 us |   654.6300 us | 2100.0000 | 800.0000 | 100.0000 | 10,230,346 B |
|     InsertRandom_SortedDict | 128000 |    36,744.720 us |   515.7820 us |   457.2273 us | 2214.2857 | 785.7143 | 142.8571 |  7,168,267 B |
|     InsertRandom_SortedList | 128000 | 1,309,206.801 us | 3,006.6702 us | 2,510.7058 us |         - |        - |        - |  4,198,264 B |
|       InsertRandom_SkipList | 128000 |    91,444.804 us |   312.5094 us |   277.0314 us | 1833.3333 | 500.0000 |        - | 10,235,732 B |
|          Enumerate_SkipList | 128000 |       441.436 us |     0.9732 us |     0.8627 us |         - |        - |        - |            - |
|              Enumerate_Dict | 128000 |     1,554.908 us |     4.1736 us |     3.4852 us |         - |        - |        - |        330 B |
|              Enumerate_List | 128000 |       492.595 us |     0.2796 us |     0.2183 us |         - |        - |        - |         57 B |

"""Adds realSize (largest dimension in metres, real-world) to WorldAssets/catalog.json.
Buildings, aircraft, bridges, terrain tiles and room dioramas keep their exhibit size in the Forest
Café world (at real scale they would cover the trails); Metro City places buildings at full scale."""
import json, sys
REAL = {
 # animals
 '04748acddb':2.4,'cbd4cbdfa9':2.4,'9e0e48b1d9':2.4,'909a7efbdd':2.0,'77e38b4a9a':1.2,'d14086203c':1.4,'f0059a3c34':1.4,'255545dae5':1.4,
 '59b924bd65':1.0,'8017170c0c':0.5,'64bff816a9':0.45,'852d149a91':0.45,'555531756b':0.12,'28d79c33af':0.45,'405a52fca6':0.5,
 'e693d06e6c':0.5,'91344a2923':0.5,'808b24611b':0.25,'2348c14afe':0.3,'de4a86ce55':0.3,'76c9fe5e1e':0.4,'7857f03cf8':1.5,'5c30d1d0d1':0.5,
 # fish
 '35478dfa2b':0.6,'14e0fe3b12':0.4,'0f5cb986e9':0.3,'7caf3306e2':0.4,'8012082742':0.6,'9c0eeb97c5':1.5,
 # vehicles (not aircraft)
 'ec2199483e':5.3,'f412ab1c19':5.0,'ce470abc08':4.9,'b32547cd57':4.8,'58ff9749b7':5.3,'97c2bb4723':4.9,'90827b67d0':5.0,
 'aa90aa5c09':4.5,'d1bcc6966b':4.5,'9137831506':4.5,'5bb93c86ab':4.5,'a4706333a0':4.5,'5af7178b03':4.5,'ccb87cb38d':4.9,
 'c0abd5e85f':4.5,'05cf2142ed':6.0,'7926f9e8dd':2.4,'4ee7d5c97f':2.3,'9de74db6b0':2.3,'0a9bb4e2c3':0.12,'7c97a0bb95':1.75,'96f31ebb8c':15.0,
 # water craft and gear
 '8cd22863d5':3.0,'b8f4bf2c59':4.5,'9e40e85f70':4.5,'755ce94ac5':2.4,
 # furniture
 'ad9b42d69f':2.4,'5edf638f35':2.4,'eec2540eee':1.6,'e31deb29a7':2.1,'6cf59f3fa5':0.9,'d3083043ca':1.0,'ba91fa76a8':2.1,'b36c9a5f03':2.8,
 '7f1f92cd97':1.5,'7296399367':2.1,'9b12a9be41':2.4,'dd317d3e66':0.9,'7ed2274162':2.1,'019c916421':0.95,'9209b63ea9':2.2,'5ff376c6ab':0.85,
 '32a2d6275e':1.1,'627316ca36':1.0,'c96a7efd2d':1.0,'f9edb98810':1.1,'930940d8f6':1.1,'354fd3cc9b':0.9,'aa82edc702':1.0,'fcb6bfff5e':2.0,
 '9b9bde8b1c':2.2,'036f673739':1.1,
 # wardrobes
 '213ef09876':2.0,'5ec5331391':2.1,'e1dbf59367':2.0,'237fb57761':2.1,'f0c25419e0':2.0,'895cd8babc':2.2,
 # food and market
 '469a8db931':0.35,'a4d6b76bda':0.35,'ec5fff3a67':0.25,'2df0fc9e70':0.3,'9910d1884c':0.5,'a6733bd590':0.12,'2ed9edf51e':0.1,
 '0cadc021f2':0.08,'e38470ce31':0.08,'6b48648a90':0.03,'4e0d38de4b':1.0,'a96c51bc37':2.5,'9a2c4cefe5':3.0,'a1b0addd9f':0.45,
 # crafts
 'ed93a499ed':1.0,'1211888641':1.0,'00f467d5f1':0.6,'fc05d414a4':1.6,
 # plants
 '7cf1a18178':0.6,'319085d7cb':0.6,'809335ca37':0.8,'c30a4c6960':0.6,'ed6f92e1a2':0.8,'14f65fbe4c':1.2,'aff9d14629':1.0,
 '334a09eb1f':12.0,'73438ba0b7':20.0,'eb1977a5e5':10.0,'03c68813dd':14.0,'3b20cbbf8c':8.0,
}
path = sys.argv[1]
items = json.load(open(path, encoding='utf-8'))
count = 0
for item in items:
    key = item['id'][5:]
    if key in REAL:
        item['realSize'] = REAL[key]; count += 1
json.dump(items, open(path, 'w', encoding='utf-8'), ensure_ascii=False, indent=2)
open(path, 'a').write('\n')
print('realSize set on', count, 'models')

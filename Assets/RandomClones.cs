using UnityEngine;

public class RandomClones : MonoBehaviour
{
    private int times = 0;
    public GameObject obj;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (times < 2000)
        {
            if (Random.value < 0.08f)
            {
                GameObject obj2 = Instantiate(obj, transform.position, Quaternion.identity);
                obj2.transform.position = new Vector3(Random.Range(-15,15), Random.Range(-15,15), 0);
                times++;
            }
        }
    }
}

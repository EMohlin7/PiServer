const host = window.location.protocol + "//" + window.location.host;
const api = host +":8080"




const jwt = localStorage.getItem("accessToken");
if(window.location.pathname != "/user_login" && window.location.pathname != "/createaccount")
{
    if(jwt != null)
    {
        Auth(jwt).then((res)=>{
            if(res.status != 200)
            {
                localStorage.removeItem("accessToken");
                window.location.assign("/user_login");
            }

            res.json().then((data)=>{document.getElementById("username").innerHTML = data["username"];});
        })
    }
    else
    {
        window.location.assign("/user_login")
    }
}


async function Auth(jwt)
{
    const res = await fetch(api + "/authjwt",{
        method: "GET",
        headers: {
            "Authorization": jwt
        }
    });
    
    return res;
}


function PopUp(text)
{
    let popUp = document.getElementById("popUp");
    if(popUp != null)
    {
        popUp.classList.remove("popUp");
        popUp.offsetWidth; //Delay
        popUp.classList.add("popUp");
        popUp.childNodes[0].nodeValue = text;
    }
    else
    {
        popUp = document.createElement("div");
        popUp.id = "popUp";
        popUp.classList.add("popUp");
        const popUpText = document.createTextNode(text);
        popUp.appendChild(popUpText);
        document.body.append(popUp);
    }
}

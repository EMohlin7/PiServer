
const host = window.location.protocol + "//" + window.location.host;

async function ApiGet(url)
{
    const res = await fetch(url);
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

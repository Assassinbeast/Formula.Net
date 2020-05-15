/**
This engine namespace is used by the framework. 
Developers shouldn't use this.
*/
export namespace Engine
{
	export class CoreManager
	{
		scriptMGR: ScriptManager;
		historyMGR: HistoryManager;
		miscMGR: MiscManager;
		viewControllerMGR: ViewControllerManager;
		webobjectMGR: WebObjectManager;
		rData: Items.RData;
		observer: MutationObserver;

		constructor()
		{
			this.scriptMGR = new ScriptManager();
			this.historyMGR = new HistoryManager();
			this.miscMGR = new MiscManager();
			this.webobjectMGR = new WebObjectManager();
			this.viewControllerMGR = new ViewControllerManager();

			this.observer = new MutationObserver(this.onMutation.bind(this));
		}
		async start()
		{
			var $rData = document.querySelector("#ff-rdata");
			this.rData = Utility.createRDataObj($rData);
			this.miscMGR.initialize(this.rData.ff_appversion, this.rData.ff_cdn);
			if (location.hash)
				Utility.scrollToHash(location.hash);

			await Utility.loadJavascriptFromRData(this.rData, async () =>
			{
				var $appElement: HTMLElement = document.querySelector("ff-app");
				Utility.preProcessHtml($appElement);

				await coreMGR.viewControllerMGR.createApp($appElement);
				coreMGR.viewControllerMGR.createViewControllers($appElement);
				coreMGR.webobjectMGR.createWebObjects($appElement);

				await coreMGR.viewControllerMGR.startApp();
				coreMGR.viewControllerMGR.startUnstartedViewControllers();
				coreMGR.webobjectMGR.startUnstartedWebObjects();

				this.observer.observe($appElement, { childList: true, subtree: true });
				(<Events.IPrivateEventHandler><any>events.onAnimatingDone).eventhandler.fireEvent();
			});
		}
		onMutation(mutations: MutationRecord[])
		{
			for (var i = 0; i < mutations.length; i++)
			{
				if (mutations[i].type == "childList")
				{
					var removedNodes = mutations[i].removedNodes;
					for (var k = 0; k < removedNodes.length; k++)
					{
						var node = removedNodes.item(k);
						if (node.nodeType == node.ELEMENT_NODE)
						{
							var $element: Element = <Element>node;
							if ($element.getAttribute("ff-webobject"))
							{
								var webobject: WebObject = $element["ff-ref"];
								coreMGR.webobjectMGR.destroyWebObject(webobject);
							}
							var childWebObjectElements = $element.querySelectorAll("[ff-webobject]");
							for (var j = 0; j < childWebObjectElements.length; j++)
							{
								var webobject: WebObject = childWebObjectElements.item(j)["ff-ref"];
								coreMGR.webobjectMGR.destroyWebObject(webobject);
							}
						}
					}
				}
			}
		}
	}
	export class HistoryManager
	{
		animateMs: number = 100;
		shallAnimate: boolean = true;
		curHistoryId: number;
		curShowingHistoryId: number;
		historyData: any;
		progressBar: Items.IProgressBar;
		changePageId: number;

		nextPopFunc: (...args: any[]) => void;
		nextPopFuncArgs: any[];
		nextActionFuncAfterAnimating: (arg: any) => void;
		nextActionFuncAfterAnimatingArg: any;

		state: HistoryManager.State;

		constructor()
		{
			this.state = HistoryManager.State.Idle;
			this.historyData = {};
			this.changePageId = 0;
			window.onpopstate = this.onPopState.bind(this);
			if (history.state != null)
				this.onWindowRestore();
			else //New init
				this.initFirstState();
		}
		private initFirstState()
		{
			this.curHistoryId = 0;
			this.curShowingHistoryId = this.curHistoryId;
			this.createHistoryData(this.curHistoryId, {
				scrollYObjects: null,
				locationKey: this.getWindowLocationKey(),
				hash: window.location.hash,
				customData: null
			});
			history.replaceState({ historyId: this.curHistoryId }, document.title);
			history.scrollRestoration = "manual";
		}
		private onWindowRestore()
		{
			//console.log("Window restored");
			this.curHistoryId = history.state.historyId;
			this.curShowingHistoryId = this.curHistoryId;
			this.createHistoryData(this.curHistoryId, {
				scrollYObjects: null,
				locationKey: this.getWindowLocationKey(),
				hash: window.location.hash,
				customData: null
			});
		}

		private onPopState(event: PopStateEvent)
		{
			//console.log("onPopState");
			//console.log("event.state", event.state)
			//Get here: Back or forward button click or manually hash appended on url bar

			//console.log("history.state.historyId: " + history.state.historyId);
			//console.log("cuHistoryId: " + this.curHistoryId);
			if (this.state == HistoryManager.State.Animating)
			{
				this.nextActionFuncAfterAnimating = this.onPopState.bind(this);
				this.nextActionFuncAfterAnimatingArg = event;
				return;
			}
			if (this.nextPopFunc != null)
			{
				//console.log("this.nextPopFunc != null");
				this.nextPopFunc(this.nextPopFuncArgs);
				this.nextPopFunc = null;
				this.nextPopFuncArgs = null;
				return;
			}

			this.changePageId++;
			//If manual # appended or 
			if (event.state == null)
				this.onManualHashAppend(); //Can be manually Hash appended(wrote on top of url or an atag didn't subscribe to event)
			else
			{
				//Can also happen if by a bug browser bug.
				//Happens when you go back from a full refresh, then move forward to a hash, then go back, then the historyStateId wont change
				/* eg like this (* means you click a link, so history getspushed  forward. < means backward button, > forward button):
					* google.com
					* /
					* /#hey
					* /about
					< /#hey
					< /
					< google.com
					> /
					> /#hey
					< / here the historyId will be the same, so now you have two histories that are the same hash,
					*/
				//You also lose all your forward history if you have more history in front of #hey
				if (history.state.historyId == this.curHistoryId) //If clicked eg back and forward before the page could be finished loading
				{
					this.curShowingHistoryId = this.curHistoryId;
					this.setState(HistoryManager.State.Idle);
				}
				else if (history.state.historyId < this.curHistoryId)
					this.onBackOrForwardClick(false);
				else
					this.onBackOrForwardClick(true);
			}
		}
		linkClick(href: string)
		{
			if (this.state == HistoryManager.State.Animating)
			{
				this.nextActionFuncAfterAnimating = this.linkClick.bind(this);
				this.nextActionFuncAfterAnimatingArg = href;
				return;
			}

			this.changePageId++;
			//console.log("linkClick");

			var aTag: HTMLAnchorElement = document.createElement("a");
			aTag.href = href;

			//If change to another website
			if (Utility.getBaseUrlOfUrl(aTag.href) !== Utility.getBaseUrlOfUrl(window.location.href))
			{
				window.location.href = href; //Change to the new website, eg https://www.google.com
				return; //No need for this, but just to be sure no wierd things will be done
			}

			if (this.state == HistoryManager.State.Changing)
			{
				this.nextPopFunc = this.linkClick.bind(this);
				this.nextPopFuncArgs = [href]
				this.goToCurShowingHistoryId();
				return;
			}

			var newLocationKey: string = this.getLocationKey(aTag.pathname, aTag.search);
			var curLocationKey = (<HistoryManager.HistoryData>this.historyData[this.curHistoryId.toString()]).locationKey;
			if (newLocationKey === curLocationKey && aTag.hash)
			{
				//console.log("Hash link click");
				var hash = Helper.textTrimStart(aTag.hash, "#");
				this.pushState(this.getWindowLocationKey() + "#" + hash);
				Utility.scrollToHash(hash);
				this.curShowingHistoryId = this.curHistoryId;
				this.setHistoryCustomData(this.getHistoryCustomData(this.curHistoryId - 1));
			}
			else
			{
				this.changePage(aTag, true);
			}
		}

		private onManualHashAppend()
		{
			if (this.state == HistoryManager.State.Changing)
			{
				this.nextPopFunc = this.onManualHashAppend.bind(this);
				this.nextPopFuncArgs = [];
				this.goToCurShowingHistoryId();
				return;
			}

			//console.log("Manually Hash appended (wrote on top of url or an atag didn't subscribe to event)");
			this.setCurrentScrollForInHistoryData(this.curHistoryId);
			this.curHistoryId++;
			this.curShowingHistoryId = this.curHistoryId;
			history.replaceState({ historyId: this.curHistoryId }, "");
			history.scrollRestoration = "manual";
			this.createHistoryData(this.curHistoryId, {
				scrollYObjects: this.getScrollYsObjects(),
				locationKey: this.getWindowLocationKey(),
				hash: window.location.hash,
				customData: this.getHistoryCustomData(this.curHistoryId - 1)
			});
		}
		private onBackOrForwardClick(isForward: boolean)
		{
			//console.log(isForward ? "Forward clicked" : "Back clicked");

			//Is true when changing to a page, and then click back/forward to go back to
			//the page it was changing to. So its like a cancel changepage
			if (history.state.historyId == this.curShowingHistoryId)
			{
				this.curHistoryId = this.curShowingHistoryId;
				this.setState(HistoryManager.State.Idle);
				return;
			}

			var changePageOnlyHash = HistoryManager.areHistoryIdPagesSiblings(
				this.curHistoryId, history.state.historyId, this.historyData);

			if (changePageOnlyHash == true)
			{
				//console.log("Back/forward to hash link");
				this.setCurrentScrollForInHistoryData(this.curHistoryId);
				this.setHistoryCustomData(this.getHistoryCustomData());
				this.curHistoryId = history.state.historyId;
				this.curShowingHistoryId = this.curHistoryId;
				if (window.location.hash)
					Utility.scrollToHash(Helper.textTrimStart(window.location.hash, "#"))
				else
					Utility.scrollLayoutAndPageScrollYObjects((<HistoryManager.HistoryData>this.historyData[this.curHistoryId.toString()]).scrollYObjects);
			}
			else //Change the page
			{
				var aTag: HTMLAnchorElement = document.createElement("a");
				aTag.href = window.location.href;
				this.changePage(aTag, false);
			}
		}

		private changePage(aTag: HTMLAnchorElement, shallPushState: boolean = true)
		{
			(<Events.IPrivateEventHandler><any>events.onChangePageBegun).eventhandler.fireEvent(aTag.href);
			if (shallPushState)
				this.pushState(aTag.href);
			else //Backward or Forward button clicked
			{
				this.curHistoryId = history.state.historyId;

				//If true, it means historyData doesn't exist even when backward clicked, 
				//because the webapp restored from another site.
				if (this.historyData[this.curHistoryId] == null)
				{
					//console.log("New historyData when backward", this.historyData[this.curHistoryId.toString()]);
					this.createHistoryData(this.curHistoryId, {
						scrollYObjects: null,
						locationKey: this.getLocationKey(aTag.pathname, aTag.search),
						hash: aTag.hash,
						customData: null
					});
				}
			}

			this.SPAPageChange1_SendRequest(aTag.href, this.changePageId);
		}
		private goToCurShowingHistoryId()
		{
			//console.log("history.go(" + (this.curShowingHistoryId - this.curHistoryId) + ")")
			var historyGo = this.curShowingHistoryId - this.curHistoryId;
			this.curHistoryId = this.curShowingHistoryId;
			this.setState(HistoryManager.State.Idle);
			history.go(historyGo);
		}
		private pushState(url: string)
		{
			this.setCurrentScrollForInHistoryData(this.curHistoryId);
			this.curHistoryId++;

			var aTag: HTMLAnchorElement = document.createElement("a");
			aTag.href = url;

			history.pushState({ historyId: this.curHistoryId }, null, url);
			history.scrollRestoration = "manual";
			this.createHistoryData(this.curHistoryId, {
				scrollYObjects: null,
				locationKey: this.getLocationKey(aTag.pathname, aTag.search),
				hash: window.location.hash,
				customData: null
			});
		}

		private SPAPageChange1_SendRequest(href: string, changePageId: number)
		{
			var pageRequestHeaders = this.getPageRequestHeaders();

			var xhr = new XMLHttpRequest();
			xhr.onreadystatechange = () =>
			{
				if (xhr.readyState != 4) //If not done
					return;
				this.SPAPageChange2_ReceiveResponse(xhr, changePageId);
			};
			var urlItems: Utility.UrlItems = Utility.getUrlItems(href);
			var urlItemsString = Utility.getHrefByUrlItems(urlItems);
			xhr.open("GET", urlItemsString == "" ? "/" : urlItemsString, true);
			xhr.setRequestHeader("ff_layout", pageRequestHeaders.layout);
			xhr.setRequestHeader("ff_pages", JSON.stringify(pageRequestHeaders.pages));
			xhr.setRequestHeader("ff_webobjects", JSON.stringify(pageRequestHeaders.webobjects));

			this.setState(HistoryManager.State.Idle);
			this.setState(HistoryManager.State.Changing);
			xhr.send();
		}
		private SPAPageChange2_ReceiveResponse(httpRequest: XMLHttpRequest, changePageId: number)
		{
			//console.log("SPAPageChange2_ReceiveResponse()")

			//If aborted (client is changing to another page while it was changing)
			if (this.changePageId != changePageId)
				return;
			if (httpRequest.status == 0) //internet crashed
			{
				this.setState(HistoryManager.State.Idle);
				throw "Couldn't retrieve the page '" + window.location.href + "'. " +
				"Either the server is down or your device has lost internet connection";
			}

			if (httpRequest.getResponseHeader("ff_redirect") == "true")
			{
				let location = httpRequest.getResponseHeader("location");
				this.linkClick(location);
				return;
			}

			var startResponse = (<string>httpRequest.response).trim().substr(0, 9).toLowerCase()
			//If the response is a whole new html page
			if (startResponse.substr(0, 9) == "<!doctype" || startResponse.substr(0, 5) == "<html")
			{
				Utility.destroyApp(httpRequest.response);
				return;
			}

			var div = document.createElement('div');
			div.innerHTML = httpRequest.response;
			var $newViewCtrl = <HTMLElement>div.children.item(0);
			var rDataDiv = div.children.item(1);

			if ($newViewCtrl == null || !($newViewCtrl.tagName == "FF-PAGE" || $newViewCtrl.tagName == "FF-LAYOUT") ||
				rDataDiv == null || rDataDiv.getAttribute("id") != "ff-rdata" ||
				(httpRequest.status >= 500 && httpRequest.status < 600))
			{
				this.setState(HistoryManager.State.Idle);
				throw "An unexpected error happened on the server.";
			}

			coreMGR.rData = Utility.createRDataObj(rDataDiv);
			if (coreMGR.rData.ff_appversion !== coreMGR.miscMGR.appVersion)
			{
				location.reload();
				return;
			}

			this.SPAPageChange3_LoadJavascript(httpRequest, $newViewCtrl, coreMGR.rData, changePageId);
		}
		private SPAPageChange3_LoadJavascript(httpRequest: XMLHttpRequest, $newViewCtrl: HTMLElement, rData: Items.RData, changePageId: number)
		{
			//TODO: put await on it?
			Utility.loadJavascriptFromRData(rData, () =>
			{
				if (this.changePageId != changePageId)
					return;
				this.SPAPageChange4_UpdateStyleDOM(httpRequest, $newViewCtrl, rData);
			});
		}

		private SPAPageChange4_UpdateStyleDOM(httpRequest: XMLHttpRequest, $newViewCtrl: HTMLElement, rData: Items.RData)
		{
			//console.log("SPAPageChange5_UpdateStyleDOM");
			//console.log(newHtml);
			//console.log(rData);

			var $webobjStyleDiv = document.getElementById("ff-webobject-styles");
			for (var i = 0; i < rData.ff_webobjectstyles.length; i++)
			{
				var styleElement = this.createElementFromHTML(rData.ff_webobjectstyles[i]);
				$webobjStyleDiv.appendChild(styleElement);
			}

			this.SPAPageChange5_UpdateNewHtmlDOM(httpRequest, $newViewCtrl, rData);
		}
		private SPAPageChange5_UpdateNewHtmlDOM(httpRequest: XMLHttpRequest, $newViewCtrl: HTMLElement, rData: Items.RData)
		{
			var $rcCtrl: Element;
			if (rData.ff_targetfoldertype == "page")
				$rcCtrl = document.querySelector("ff-page[ff-name='" + rData.ff_targetfolderpagename + "']");
			else if (rData.ff_targetfoldertype == "layout")
				$rcCtrl = document.querySelector("ff-layout");
			else if (rData.ff_targetfoldertype == "app")
				$rcCtrl = document.querySelector("ff-app");
			else
				throw "No other targetfoldertype";

			var $rcFolder: HTMLElement = $rcCtrl.querySelector("ff-folder");

			this.setCurrentScrollForInHistoryData(this.curShowingHistoryId);
			var $deadViewCtrl: HTMLElement = <HTMLElement>$rcFolder.firstElementChild;
			var $dyingWebObjects = $deadViewCtrl.querySelectorAll("[ff-webobject]");
			//console.log($dyingWebObjects);
			for (var i = 0; i < $dyingWebObjects.length; i++)
			{
				var webobject = $dyingWebObjects.item(i)["ff-ref"];
				coreMGR.webobjectMGR.destroyWebObject(webobject);
			}
			coreMGR.viewControllerMGR.destroyViewControllers($deadViewCtrl);

			if (this.shallAnimate == true)
			{
				$deadViewCtrl.classList.add("ff-anim-exit");
				$deadViewCtrl.appendChild(this.createElementFromHTML("<div class='ff-deadviewctrl-overlay'></div>"));
			}
			else
				$rcFolder.removeChild($deadViewCtrl);

			Utility.replaceDeadScriptsWithWorkingScripts($newViewCtrl);

			if (this.shallAnimate == true)
				$newViewCtrl.classList.add("ff-anim-enter");
			$rcFolder.appendChild($newViewCtrl);

			this.setState(HistoryManager.State.Animating);

			Utility.preProcessHtml($newViewCtrl);

			coreMGR.viewControllerMGR.createViewControllers($newViewCtrl);
			coreMGR.webobjectMGR.createWebObjects($newViewCtrl);

			coreMGR.viewControllerMGR.startUnstartedViewControllers();
			coreMGR.webobjectMGR.startUnstartedWebObjects();

			setTimeout(() =>
			{
				if (this.shallAnimate == true)
				{
					$newViewCtrl.classList.remove("ff-anim-enter");
					$rcFolder.removeChild($deadViewCtrl);
				}
				this.setState(HistoryManager.State.Idle);
				(<Events.IPrivateEventHandler><any>events.onAnimatingDone).eventhandler.fireEvent();
				HistoryManager.CssUtility.removeUnusuedWebObjectCss();

				if (this.nextActionFuncAfterAnimating != null)
				{
					this.nextActionFuncAfterAnimating(this.nextActionFuncAfterAnimatingArg);
					this.nextActionFuncAfterAnimating = null;
					return;
				}
			}, this.shallAnimate == true ? this.animateMs : 0);

			this.SPAPageChange6_LastCode(httpRequest, rData, $rcFolder, $newViewCtrl);
		}
		private SPAPageChange6_LastCode(httpRequest: XMLHttpRequest, rData: Items.RData, $rcFolder: HTMLElement, $newViewCtrl: HTMLElement)
		{
			//console.log("SPAPageChange6_LastCode()")

			document.title = rData.ff_title;
			this.curShowingHistoryId = this.curHistoryId;
			(<Events.IPrivateEventHandler><any>events.onChangePageDone).eventhandler.fireEvent($newViewCtrl);

			if (window.location.hash)
			{
				Utility.scrollToHash(window.location.hash);
			}
			else
			{
				//If back or forward button
				if (this.historyData[this.curHistoryId.toString()] != null &&
					(<HistoryManager.HistoryData>this.historyData[this.curHistoryId.toString()]).scrollYObjects != null)
					Utility.scrollLayoutAndPageScrollYObjects((<HistoryManager.HistoryData>this.historyData[this.curHistoryId.toString()]).scrollYObjects);
				else
				{
					Utility.scrollToTarget($rcFolder, rData.ff_scrollyextraspace != null ? rData.ff_scrollyextraspace : 0, true);
				}
			}
		}

		private createHistoryData(historyId: number, historyDataItem: HistoryManager.HistoryData)
		{
			this.historyData[historyId] = historyDataItem;
		}

		private setCurrentScrollForInHistoryData(historyId: number)
		{
			(<HistoryManager.HistoryData>this.historyData[historyId.toString()]).scrollYObjects = this.getScrollYsObjects();
		}
		private getScrollYsObjects()
		{
			function addItem($element: HTMLElement, key: string)
			{
				if ($element.scrollTop != null && $element.scrollTop != 0)
					scrollYObjects[key] = $element.scrollTop;
			}

			let scrollYObjects: any = {};
			addItem(document.documentElement, "_html");
			addItem(document.body, "_body");
			addItem(coreMGR.viewControllerMGR.$layout, "_layout");

			let $pages = coreMGR.viewControllerMGR.$pages;
			for (var pageName in $pages)
			{
				var $page = $pages[pageName];
				addItem($page, pageName);
			}

			return scrollYObjects;
		}

		setState(newState: HistoryManager.State)
		{
			if (this.progressBar != null)
			{
				if (this.state == HistoryManager.State.Changing)
				{
					if (newState != HistoryManager.State.Changing)
						this.progressBar.done();
				}
				else //Current state is idle or animating
				{
					if (newState == HistoryManager.State.Changing)
						this.progressBar.start();
				}
			}
			this.state = newState;
		}
		getWindowLocationKey()
		{
			return window.location.pathname + window.location.search;
		}
		getLocationKey(pathName: string, search: string)
		{
			return pathName + search;
		}
		createElementFromHTML(htmlString: string): HTMLElement
		{
			var div = document.createElement('div');
			div.innerHTML = htmlString.trim();
			return <HTMLElement>div.firstChild;
		}
		getPageRequestHeaders(): HistoryManager.PageRequestHeaders
		{
			var layout = document.getElementsByTagName("ff-layout").item(0);
			var pages = document.getElementsByTagName("ff-page");
			var webobjects = document.querySelectorAll("[ff-webobject]");

			var layoutName = layout.getAttribute("ff-name");
			var pageNames = [];
			for (var i = 0; i < pages.length; i++)
				pageNames.push(pages.item(i).getAttribute("ff-name"));
			var webobjectNames = [];
			for (var i = 0; i < webobjects.length; i++)
				webobjectNames.push(webobjects.item(i).getAttribute("ff-webobject"));

			return {
				layout: layoutName,
				pages: pageNames,
				webobjects: webobjectNames
			};
		}
		setHistoryCustomData(data: any, historyId: number = null)
		{
			var siblingHistoryIds: number[];
			if (historyId == null)
				siblingHistoryIds = HistoryManager.getHistoryIdSiblings(this.curHistoryId, this.historyData);
			else
				siblingHistoryIds = HistoryManager.getHistoryIdSiblings(historyId, this.historyData);

			for (var i = 0; i < siblingHistoryIds.length; i++)
				(<HistoryManager.HistoryData>this.historyData[siblingHistoryIds[i].toString()]).customData = data;

		}
		getHistoryCustomData(historyId: number = null)
		{
			if (historyId == null)
				return (<HistoryManager.HistoryData>this.historyData[this.curHistoryId.toString()]).customData;
			else
				return (<HistoryManager.HistoryData>this.historyData[historyId.toString()]).customData;
		}

		static getHistoryIdSiblings(historyId: number, historyData: any): number[]
		{
			//HistoryIds are siblings if you for example are in this page
			//"/foo" and then you click hashlink "/foo#bar", then they have different historyIds,
			//but they are siblings, because the page didn't change. Because hashclinks are only scrolling.
			//This is used if developer wants to use custom data for a historyId.
			//And then added the data eg: foo: 100, then if user clicks #bar hashlink
			//Then the data should still be foo: 100, even thoguh the historyId has changed
			//Because only hash changed.

			//So when the developer wants to add/edit/retrieve custom data, 
			//then all siblings historyIds must have the same custom data


			var minSiblingId: number;
			var maxSiblingId: number

			var curHistoryId = historyId;
			while (true)
			{
				if ((curHistoryId - 1).toString() in historyData == false)
					break;
				if (this.areHistoryIdPagesSiblings(curHistoryId, curHistoryId - 1, historyData) == false)
					break;
				curHistoryId--;
			}
			minSiblingId = curHistoryId;

			curHistoryId = historyId;
			while (true)
			{
				if ((curHistoryId + 1).toString() in historyData == false)
					break;
				if (this.areHistoryIdPagesSiblings(curHistoryId, curHistoryId + 1, historyData) == false)
					break
				curHistoryId++;
			}
			maxSiblingId = curHistoryId;

			if (minSiblingId == maxSiblingId)
				return [historyId];

			var siblings: number[] = [];
			for (var i = minSiblingId; i <= maxSiblingId; i++)
				siblings.push(i);
			return siblings;
		}
		static areHistoryIdPagesSiblings(prevHistoryId: number, newHistoryId, historyData: any): boolean
		{
			if (prevHistoryId === newHistoryId)
				throw "prevHistoryId and newHistoryId cant be the same";

			/* /hi -> /hi#yay -> /hi#yay2 -> /hi */

			//If back
			if (newHistoryId < prevHistoryId)
			{
				//CHeck if historyData contains all the historyIds
				for (var curHistoryId: any = newHistoryId; curHistoryId <= prevHistoryId; curHistoryId++)
					if (curHistoryId.toString() in historyData == false)
						return false;

				if (!historyData[prevHistoryId.toString()].hash)
					return false;
				if (historyData[newHistoryId.toString()].locationKey !=
					historyData[prevHistoryId.toString()].locationKey)
					return false;

				var i = prevHistoryId - 1;
				while (i > newHistoryId)
				{
					if (!(historyData[i.toString()].locationKey ==
						historyData[prevHistoryId.toString()].locationKey &&
						historyData[i.toString()].hash))
						return false;
					i--;
				}

				return true;
			}
			else //forward
			{
				//CHeck if historyData contains all the historyIds
				for (var curHistoryId: any = prevHistoryId; curHistoryId <= newHistoryId; curHistoryId++)
					if (curHistoryId.toString() in historyData == false)
						return false;

				var i = prevHistoryId + 1;
				while (i <= newHistoryId)
				{
					if (!(historyData[i.toString()].hash &&
						historyData[prevHistoryId.toString()].locationKey == historyData[i.toString()].locationKey))
						return false;
					i++;
				}

				return true;
			}
		}
	}
	export namespace HistoryManager
	{
		export class CssUtility
		{
			static getDeletingStyles(ctrlType: string, pageName?: string): any
			{
				var rcElements: MiscManager.RcElements = HistoryManager.CssUtility.getUniqueRcElementsUnderCtrlType(ctrlType, pageName);
				var allCurCss: string[] = HistoryManager.CssUtility.getAllCurrentCss();

				//console.log(allCurCss);

				var cssLayoutDir: string;
				if (rcElements.layout != null)
					cssLayoutDir = HistoryManager.CssUtility.getCssFilePathDir("layout", rcElements.layout);

				var cssPageDirs: string[] = [];
				for (var prop in rcElements.pages)
					cssPageDirs.push(HistoryManager.CssUtility.getCssFilePathDir("page", prop));

				var cssWebObjectDirs: string[] = [];
				for (var prop in rcElements.webobjects)
					cssWebObjectDirs.push(HistoryManager.CssUtility.getCssFilePathDir("webobject", prop));

				//console.log(cssLayoutDir);
				//console.log(cssPageDirs);
				//console.log(cssWebObjectDirs);

				var deletingStyles: any = {}

				//Add layout css to deletingStyles
				if (cssLayoutDir != null)
				{
					for (var i = 0; i < allCurCss.length; i++)
						if (allCurCss[i].substr(0, cssLayoutDir.length) == cssLayoutDir)
							deletingStyles[allCurCss[i]] = null;
				}


				//Add pages css to deletingStyles
				for (var i = 0; i < allCurCss.length; i++)
					for (var k = 0; k < cssPageDirs.length; k++)
					{
						if (allCurCss[i].substr(0, cssPageDirs[k].length) == cssPageDirs[k])
							deletingStyles[allCurCss[i]] = null;
					}

				//Add webobjects css to deletingStyles
				for (var i = 0; i < allCurCss.length; i++)
					for (var k = 0; k < cssWebObjectDirs.length; k++)
					{
						if (allCurCss[i].substr(0, cssWebObjectDirs[k].length) == cssWebObjectDirs[k])
							deletingStyles[allCurCss[i]] = null;
					}

				//console.log(deletingStyles);
				return deletingStyles;
			}
			static getLastStyleElementOfType(styles: any, styleTypeFirstChar: string)
			{
				var targetKey: string = null;
				for (var key in styles)
				{
					if (key.charAt(0) == styleTypeFirstChar)
						targetKey = key;
				}
				if (targetKey == null)
					return null;

				return styles[targetKey];
			}
			static getAllCurrentCss()
			{
				var stylesObj: string[] = [];
				var styles: NodeListOf<Element> = document.head.querySelectorAll("style[data-path]");
				for (var i = 0; i < styles.length; i++)
					stylesObj.push(styles.item(i).getAttribute("data-path"));
				return stylesObj;
			}
			static getUniqueRcElementsUnderCtrlType(ctrlType: string, pageName?: string): MiscManager.RcElements
			{
				var rcElements: MiscManager.RcElements = {};

				if (ctrlType == "app")
				{
					var $appElement = document.querySelector("ff-app");
					var $layout = $appElement.querySelector("ff-layout");
					var $pages = $appElement.querySelectorAll("ff-page");
					var $webobjects = $appElement.querySelectorAll("[ff-webobject]");

					rcElements.layout = $layout.getAttribute("ff-name");
					rcElements.pages = {};
					for (var i = 0; i < $pages.length; i++)
						rcElements.pages[$pages[i].getAttribute("ff-name")] = null;
					rcElements.webobjects = {};
					for (var i = 0; i < $webobjects.length; i++)
						rcElements.webobjects[$webobjects[i].getAttribute("ff-webobject")] = null;
					return rcElements;
				}
				else if (ctrlType == "layout")
				{
					var $layoutElement = document.querySelector("ff-layout");
					var $pages = $layoutElement.querySelectorAll("ff-page");
					var $webobjects = $layoutElement.querySelectorAll("[ff-webobject]");

					rcElements.pages = {};
					for (var i = 0; i < $pages.length; i++)
						rcElements.pages[$pages[i].getAttribute("ff-name")] = null;
					rcElements.webobjects = {};
					for (var i = 0; i < $webobjects.length; i++)
						rcElements.webobjects[$webobjects[i].getAttribute("ff-webobject")] = null;
				}
				else if (ctrlType == "page")
				{
					if (pageName == null)
						throw "MiscManager.getDeletingRcElements() pageName is null"

					var $pageElement = document.querySelector("ff-page[ff-name='" + pageName + "']");

					var $pages = $pageElement.querySelectorAll("ff-page");
					var $webobjects = $pageElement.querySelectorAll("[ff-webobject]");

					rcElements.pages = {};
					for (var i = 0; i < $pages.length; i++)
						rcElements.pages[$pages[i].getAttribute("ff-name")] = null;
					rcElements.webobjects = {};
					for (var i = 0; i < $webobjects.length; i++)
						rcElements.webobjects[$webobjects[i].getAttribute("ff-webobject")] = null;
				}
				else
					throw "MiscManager.getDeletingRcElements() ctrlType key was invalid. ctrlType was: " + ctrlType;

				return rcElements;
			}
			static getCssFilePathDir(rcElementType: string, rcElementName?: string)
			{
				if (rcElementName != null)
					rcElementName = rcElementName.toLowerCase();

				if (rcElementType == "layout") 
				{
					//rcElementName eg "Main"
					return "layouts/" + rcElementName;
				}
				else if (rcElementType == "page") 
				{
					//rcElementName eg "_empty" or "account._empty
					return "pages/" + Helper.textReplaceAll(rcElementName, ".", "/"); //account/_empty
				}
				else if (rcElementType == "webobject")
				{
					//rcElementName eg "calendar" or "menus.sideMenu
					return "webobjects/" + Helper.textReplaceAll(rcElementName, ".", "/");
				}
			}
			static removeUnusuedWebObjectCss()
			{
				var webobjectNames: any = {}; //HashSet. All current webobjects present in the DOM
				var webobjects = document.querySelectorAll("[ff-webobject]");
				for (var i = 0; i < webobjects.length; i++)
					webobjectNames[webobjects.item(i).getAttribute("ff-webobject")] = null;

				//<WebObjectName, StyleElements>. All current webobject styles in the ff-webobject-styles
				var webobjectStyles: any = {};
				var $webobjectStyles = document.getElementById("ff-webobject-styles").children;
				for (var i = 0; i < $webobjectStyles.length; i++)
				{
					var name = $webobjectStyles.item(i).getAttribute("data-webobject-name");
					if (name in webobjectStyles)
						(<Element[]>webobjectStyles[name]).push($webobjectStyles.item(i));
					else
						webobjectStyles[name] = [$webobjectStyles.item(i)];
				}
				for (var webobjStyle in webobjectStyles)
				{
					if (webobjStyle in webobjectNames == false)
					{
						var removingStyles = <Element[]>webobjectStyles[webobjStyle];
						for (var i = 0; i < removingStyles.length; i++)
							removingStyles[i].parentElement.removeChild(removingStyles[i]);
					}
				}
			}
		}
		export interface PageRequestHeaders
		{
			layout: string;
			pages: string[];
			webobjects: string[];
		}
		export interface HistoryData
		{
			scrollYObjects: any; //{ "_html", "_body", "_layout": 20, "Account.Settings": 300 }
			locationKey: string; //pathname + search
			hash: string;
			customData: any;
		}
		export enum State
		{
			Idle, Changing, Animating
		}
	}
	export class ScriptManager
	{
		//Note, we use the first src in the collection of fallbacks to identify a loadedJs

		loadedJs: any; //Key is name
		constructor()
		{
			this.loadedJs = {};
		}

		async loadJavascripts(srcs: Items.ScriptItem[], onDone: (fails: string[]) => void)
		{
			var fails: string[] = [];
			var loadCount: number = 0;

			for (var i = 0; i < srcs.length; i++)
			{
				if (srcs[i].path in this.loadedJs)
				{
					loadCount++;
					if (loadCount == srcs.length)
						await onDone(fails);
				}
				else
				{
					await this.loadJavascript(srcs[i].path, srcs[i].isModule, async (success: boolean, src: string) =>
					{
						if (success == false)
							fails.push(src);
						loadCount++;
						if (loadCount == srcs.length)
							await onDone(fails);
					});
				}
			}
		}

		async loadJavascript(src: string, isModule: boolean,
			onDone: (success: boolean, src: string) => void)
		{
			var script = document.createElement('script');
			script.src = src;
			script.async = false;
			if (isModule == true)
				script.type = "module";
			script.onload = async () =>
			{
				this.loadedJs[src] = null;
				await onDone(true, src);
			}
			script.onerror = async () => { await onDone(false, src); }
			document.head.appendChild(script);
		}
	}
	export class MiscManager
	{
		appVersion: number;
		cdn: string;
		isSmoothScrollSupported: boolean;
		hashScrollExtraSpace: number;
		uId: number;

		constructor()
		{
			this.isSmoothScrollSupported = "scrollBehavior" in document.documentElement.style;
			this.hashScrollExtraSpace = 0;
			this.uId = 0;
		}
		initialize(appVersion: number, cdn: string)
		{
			this.appVersion = appVersion;
			this.cdn = cdn;
		}
		createUId()
		{
			this.uId++;
			return this.uId.toString();
		}
	}
	export namespace MiscManager
	{
		export interface RcElements
		{
			layout?: string;
			pages?: any; //HashSet
			webobjects?: any; //HashSet
		}
	}
	export class ViewControllerManager
	{
		$layout: HTMLElement;
		get layout(): BaseLayout
		{
			return this.$layout["ff-ref"];
		}

		pages: BasePage[];
		$pages: any; //key PageName, Value: HTMLElement

		unstartedLayout: BaseLayout;
		unstartedPages: BasePage[];

		constructor()
		{
			this.pages = [];
			this.$pages = {};
			this.unstartedPages = [];
		}

		getFullLayoutClassName(layoutName: string): string
		{
			return "Layouts." + layoutName + "." + layoutName + "Layout";
		}
		getFullPageClassName(pageName: string): string
		{
			return "Pages." + pageName + "." + pageName + "Page";
		}

		async createApp($appElement: HTMLElement)
		{
			let module = null;
			try
			{
				module = await import('../app/app.js');
			}
			catch (e)
			{
				//TODO show exception page
				console.log(e);
			}

			new module.InitializeApp();
			var app = module.app;
			if (app instanceof BaseApp == false)
				throw "App class must be extended from th.BaseApp";

			app.$element = $appElement;
			app.$element["ff-ref"] = this.app;
			app.id = coreMGR.miscMGR.createUId();
			if ("thAwake" in app)
				(<any>app).thAwake();
		}
		async startApp()
		{
			let module = await import('../app/app.js');
			if ("thStart" in module.app)
				(<any>module.app).thStart();
		}
		createViewControllers($parentElement: HTMLElement)
		{
			let $layout: HTMLElement = $parentElement.tagName == "FF-LAYOUT" ? $parentElement : $parentElement.querySelector("ff-layout");
			if ($layout != null)
			{
				this.$layout = $layout;
				var fullClassName = this.getFullLayoutClassName($parentElement.getAttribute("ff-name"));
				var classObj = Utility.getClassObjFromString(fullClassName);
				if (classObj != null)
				{
					var layout: BaseLayout = new classObj();
					if (layout instanceof BaseLayout == false)
						throw "The Layout class " + fullClassName + " must be extended from th.BaseLayout";

					this.unstartedLayout = layout;
					layout.$element = this.$layout;
					layout.id = coreMGR.miscMGR.createUId();
					this.$layout["ff-ref"] = layout;
					if ("thAwake" in this.layout)
						(<any>layout).thAwake();
				}
			}

			var $pages: HTMLElement[] = []
			if ($parentElement.tagName == "FF-PAGE")
				$pages.push($parentElement);
			var $childPages: NodeListOf<HTMLElement> = <any>$parentElement.querySelectorAll("ff-page");
			for (let i = 0; i < $childPages.length; i++)
				$pages.push($childPages.item(i));
			for (let i = 0; i < $pages.length; i++)
			{
				let $page = $pages[i];
				let pageName = $page.getAttribute("ff-name");
				this.$pages[pageName] = $page;
				let fullClassName = this.getFullPageClassName(pageName);
				let classObj = Utility.getClassObjFromString(fullClassName);
				if (classObj != null)
				{
					let page: BasePage = new classObj();
					if (page instanceof BasePage == false)
						throw "The Page class " + fullClassName + " must be extended from th.BasePage";

					this.pages.push(page);
					this.unstartedPages.push(page);

					page.$element = $page;
					page.id = coreMGR.miscMGR.createUId();
					$page["ff-ref"] = page;
					if ("thAwake" in page)
						(<any>page).thAwake();
				}
			}
		}
		startUnstartedViewControllers()
		{
			if (this.unstartedLayout != null && "thStart" in this.unstartedLayout)
			{
				(<any>this.unstartedLayout).thStart();
				this.unstartedLayout = null;
			}

			for (var i = 0; i < this.unstartedPages.length; i++)
			{
				var page: BasePage = this.unstartedPages[i];
				if ("thStart" in page)
					(<any>page).thStart();
			}
			this.unstartedPages.length = 0;
		}

		destroyViewControllers($deadViewCtrl: HTMLElement)
		{
			var $viewCtrls: HTMLElement[] = [$deadViewCtrl];
			var $childControllers: NodeListOf<HTMLElement> = <any>$deadViewCtrl.querySelectorAll("ff-layout, ff-page");
			for (var i = 0; i < $childControllers.length; i++)
				$viewCtrls.push($childControllers.item(i));
			for (var i = $viewCtrls.length - 1; i >= 0; i--)
			{
				var $viewCtrl = $viewCtrls[i];
				if ($viewCtrl.tagName == "FF-LAYOUT")
					this.$layout = null;
				else
					delete this.$pages[$viewCtrl.getAttribute("ff-name")];

				var viewCtrl: ViewController = $viewCtrls[i]["ff-ref"];
				if (viewCtrl == null)
					continue;
				this.destroyViewController(viewCtrl);
			}
		}
		destroyViewController(viewController: ViewController)
		{
			if (viewController.isAlive == false)
				return;
			(<any>viewController)._isAlive = false;
			if ("thDestroy" in viewController)
				(<any>viewController).thDestroy();
		}
	}
	export class WebObjectManager
	{
		webobjects_id: any;
		webobjects_type: any;
		unstartedWebobjects: WebObject[];

		constructor()
		{
			this.webobjects_id = {}
			this.webobjects_type = {};
			this.unstartedWebobjects = [];
		}
		createWebObjects($parentObj: HTMLElement): WebObject[]
		{
			var webobjects: WebObject[] = [];
			if ($parentObj.getAttribute("ff-webobject"))
				this.createWebObject($parentObj);

			var $webobjects = $parentObj.querySelectorAll("[ff-webobject]");
			for (var i = 0; i < $webobjects.length; i++)
			{
				var newWebObject: WebObject = this.createWebObject(<HTMLElement>$webobjects.item(i));
				webobjects.push(newWebObject);
			}

			return webobjects;
		}
		startUnstartedWebObjects()
		{
			for (var i = 0; i < this.unstartedWebobjects.length; i++)
			{
				if ("thStart" in this.unstartedWebobjects[i])
					(<any>this.unstartedWebobjects[i]).thStart();
			}
			this.unstartedWebobjects.length = 0;
		}
		createWebObject($webobject: HTMLElement): WebObject
		{
			if ($webobject["ff-ref"] != null)
				return;

			var webobjectName = $webobject.getAttribute("ff-webobject");
			var classObj = Utility.getClassObjFromString(this.getWebObjectFullClassName(webobjectName));
			var webobjectInstance: WebObject = null;
			if (classObj == null)
				throw "A WebObject couldn't be instantiated. The class '" + this.getWebObjectFullClassName(webobjectName) + "' doesn't exist";

			webobjectInstance = new classObj();
			if (webobjectInstance instanceof WebObject == false)
				throw "The WebObject " + webobjectName + " must be extended from th.WebObject";

			webobjectInstance.$element = $webobject;
			webobjectInstance.id = coreMGR.miscMGR.createUId();

			webobjectInstance.$element["ff-ref"] = webobjectInstance;
			this.webobjects_id[webobjectInstance.id] = webobjectInstance;
			if (webobjectInstance.name in this.webobjects_type == false)
				this.webobjects_type[webobjectInstance.name] = {};
			this.webobjects_type[webobjectInstance.name][webobjectInstance.id] = webobjectInstance;

			this.unstartedWebobjects.push(webobjectInstance);
			if ("thAwake" in webobjectInstance)
				(<any>webobjectInstance).thAwake();

			return webobjectInstance;
		}

		destroyWebObject(webobject: WebObject)
		{
			if (webobject.isAlive == false)
				return;
			(<any>webobject)._isAlive = false;
			if ("thDestroy" in webobject)
				(<any>webobject).thDestroy();
			delete this.webobjects_id[webobject.id];
			delete this.webobjects_type[webobject.name][webobject.id];
		}

		getWebObjectFullClassName(webobjectName: string): string
		{
			//webobjectName eg: Calendar or Foods.Burger
			var nameSegments = webobjectName.split(".");
			var firstName = nameSegments[nameSegments.length - 1];
			return "WebObjects." + webobjectName + "." + firstName + "WebObject";
		}
	}
	export class Utility
	{
		static getWindowScrollY(): number
		{
			return document.documentElement.scrollTop;
		}
		static getWindowScrollX(): number
		{
			return document.documentElement.scrollLeft;
		}
		static scrollToHash(hash: string)
		{
			hash = Helper.textTrimStart(hash, "#");
			var element = document.getElementById(hash);
			if (element == null)
				return;
			this.scrollToTarget(element, coreMGR.miscMGR.hashScrollExtraSpace);
		}
		static scrollLayoutAndPageScrollYObjects(layoutAndPageScrollYObjects: any): void
		{
			//console.log(layoutAndPageScrollYObjects);
			//layoutAndPageScrollYObjects  ["_html", "_body", "_layout", "Account.Settings"]
			function getPageByName(pageName: string)
			{
				for (let i = 0; i < coreMGR.viewControllerMGR.pages.length; i++)
				{
					var page = coreMGR.viewControllerMGR.pages[i];
					if (page.name == pageName)
						return page;
				}
				return null;
			}

			for (let key in layoutAndPageScrollYObjects)
			{
				if (key == "_html")
				{
					let $html = document.documentElement;
					let y = layoutAndPageScrollYObjects["_html"];
					this.scrollElement($html, y);
				}
				else if (key == "_body")
				{
					let $body = document.body;
					let y = layoutAndPageScrollYObjects["_body"];
					this.scrollElement($body, y);
				}
				else if (key == "_layout")
				{
					let $layout = coreMGR.viewControllerMGR.$layout;
					let y = layoutAndPageScrollYObjects["_layout"];
					this.scrollElement($layout, y);
				}
				else //its a page
				{
					var $page = coreMGR.viewControllerMGR.$pages[key];
					if ($page != null)
					{
						let y = layoutAndPageScrollYObjects[key];
						this.scrollElement($page, y);
					}
				}
			}
		}
		static preProcessHtml($parentObj: HTMLElement)
		{
			this.initATags($parentObj);
			(<Events.IPrivateEventHandler><any>events.onInitObjects).eventhandler.fireEvent($parentObj);
		}
		static initATags($parentObj: HTMLElement)
		{
			var subscribeATagOnClick = function ($aTag: HTMLAnchorElement)
			{
				if ($aTag["ff-atag-init"] != null)
					return;

				$aTag.onclick = () =>
				{
					coreMGR.historyMGR.linkClick($aTag.href);
					return false;
				}
				$aTag["ff-atag-init"] = true;
			}
			if ($parentObj.tagName == "A" && $parentObj.getAttribute("ff-ignore") == null)
				subscribeATagOnClick(<HTMLAnchorElement>$parentObj);

			var $aTags = $parentObj.getElementsByTagName("a");
			for (var i = 0; i < $aTags.length; i++)
			{
				if ($aTags.item(i).getAttribute("ff-ignore") == null)
					subscribeATagOnClick($aTags.item(i));
			}
		}
		static getPolyfillsScripts(rData: Items.RData): string[]
		{
			if (rData.ff_polyfills == null)
				return null;

			var polyfillScripts: string[] = [];
			if (rData.ff_polyfills)
			{
				if (rData.ff_polyfills.evaluation != null)
				{
					var evalItems = rData.ff_polyfills.evaluation;
					for (let i = 0; i < evalItems.length; i++)
					{
						var evalItem = evalItems[i];
						if (Utility.getClassObjFromString(evalItem.folderName) == null)
						{
							for (let j = 0; j < evalItem.jsFiles.length; j++)
								polyfillScripts.push(evalItem.jsFiles[j]);
						}
					}
				}

				if (rData.ff_polyfills.modernizr && window["Modernizr"])
				{
					var modernizr = window["Modernizr"];
					var modItems = rData.ff_polyfills.modernizr;
					for (let i = 0; i < modItems.length; i++)
					{
						let modItem = modItems[i];
						if (modItem.folderName in modernizr)
						{
							if (modernizr[modItem.folderName] == false)
							{
								for (var j = 0; j < modItem.jsFiles.length; j++)
									polyfillScripts.push(modItem.jsFiles[j]);
							}
						}
						else
						{
							console.error("You have created a Modernizr Polyfill folder '" +
								modItem.folderName + "' but you haven't build it into the Modernizr object. " +
								"Modernizr." + modItem.folderName + " is undefined. " +
								"Build it at https://modernizr.com/download?setclasses");
						}
					}
				}

				return polyfillScripts;
			}
		}
		static async loadJavascriptFromRData(rData: Items.RData, onDone?: () => void)
		{
			var scripts = rData.ff_scripts;
			var srcs: Items.ScriptItem[] = [];
			for (let i = 0; i < scripts.length; i++)
			{
				srcs.push({
					path: scripts[i].path,
					isModule: scripts[i].isModule
				});
			}

			var polyfillScripts: string[] = this.getPolyfillsScripts(rData);
			if (polyfillScripts != null)
			{
				for (let i = 0; i < polyfillScripts.length; i++)
				{
					srcs.push({
						path: polyfillScripts[i],
						isModule: false
					});
				}
			}

			await coreMGR.scriptMGR.loadJavascripts(srcs, (fails: string[]) =>
			{
				if (fails.length > 0)
				{
					var errorText = "";
					for (let i = 0; i < fails.length; i++)
					{
						errorText += fails[i];
						if (i + 1 < fails.length)
							errorText += ", ";
					}
					throw "The following javascript(s) was failed to load:\n\n" + errorText;
				}

				if (onDone != null)
					onDone();
			});
		}
		static createRDataObj($rData: Element): Items.RData
		{
			var jsonText = atob($rData.getAttribute("data-x"))
			return JSON.parse(jsonText);
		}
		static getClassObjFromString(fullName: string): any
		{
			var nameItems: string[] = fullName.split(".");
			var curObj = window;
			for (var i = 0; i < nameItems.length; i++)
			{
				curObj = curObj[nameItems[i]];
				if (curObj == null)
					return null;
			}
			return curObj;
		}
		static getBaseUrlOfUrl(url)
		{
			var pathArray = url.split('/');
			var protocol = pathArray[0];
			var host = pathArray[2];
			var baseUrl = protocol + '//' + host;
			return baseUrl;
		}
		static getUrlItems(href: string): Utility.UrlItems
		{
			var a: HTMLAnchorElement = document.createElement("a");
			a.href = href;
			var hash: string = "";
			if (a.hash)
				hash = a.hash.substr(1);
			var query = this.getQueryDicFromATag(a)
			return {
				path: a.pathname,
				hash: hash,
				query: query
			}
		}
		static getQueryDicFromATag(aTag: HTMLAnchorElement): any
		{
			var search = aTag.search;
			if (!search)
				return {};
			search = search.substr(1);
			var items: string[] = search.split("&");
			var query: any = {};
			for (var i = 0; i < items.length; i++)
			{
				var keyVal: string[] = items[i].split("=");
				query[keyVal[0]] = keyVal[1];
			}
			return query;
		}
		static getHrefByUrlItems(urlItems: Utility.UrlItems)
		{
			var href = urlItems.path;
			if (Object.keys(urlItems.query).length >= 1)
			{
				href += "?";
				for (var key in urlItems.query)
					href += key + "=" + urlItems.query[key] + "&";
				href = href.substr(0, href.length - 1);
			}
			if (urlItems.hash)
				href += "#" + urlItems.hash;
			return href;
		}

		static scrollToTarget(target: HTMLElement, extraSpace: number = 0, onlyIfBelow: boolean = false)
		{
			var targetY = target.getBoundingClientRect().top;
			var relativeTarget = target.parentElement;
			while (true)
			{
				var relativeY = (targetY - extraSpace) - relativeTarget.getBoundingClientRect().top;

				if (onlyIfBelow)
				{
					if (relativeY < 0 && relativeTarget.scrollTop > 0)
					{
						this.scrollElement(relativeTarget, relativeTarget.scrollTop + relativeY);
						targetY = targetY - relativeY;
					}
				}
				else
				{
					if (this.isElementOverflowing(relativeTarget))
					{
						this.scrollElement(relativeTarget, relativeTarget.scrollTop + relativeY);
						targetY = targetY - relativeY;
					}
				}
				relativeTarget = relativeTarget.parentElement;
				if (relativeTarget == null)
					break;
			}
		}
		static isElementOverflowing(el)
		{
			var curOverflow = el.style.overflow;

			if (!curOverflow || curOverflow === "visible")
				el.style.overflow = "hidden";

			var isOverflowing = el.clientWidth < el.scrollWidth
				|| el.clientHeight < el.scrollHeight;

			el.style.overflow = curOverflow;

			return isOverflowing;
		}
		static scrollElement(element: HTMLElement, y: number)
		{
			if (element.scrollTo)
				element.scrollTo({ top: y, behavior: "smooth" });
			else
				element.scrollTop = y;
		}

		static removeAllAttributes(element: HTMLElement)
		{
			for (var i = 0; i < element.attributes.length; i++)
				element.removeAttribute(element.attributes[i].name);
		}
		static copyAttributesToElement(srcElement: HTMLElement, targetElement: HTMLElement)
		{
			for (var i = 0; i < srcElement.attributes.length; i++)
				targetElement.setAttribute(srcElement.attributes[i].name, srcElement.getAttribute(srcElement.attributes[i].name));
		}
		static replaceDeadScriptsWithWorkingScripts($parentElement: Element)
		{
			//its because if a element have <script>console.log("hello world");</script>
			//Then it actually wont work.
			//We have to make a whole new script element and then replace the existing
			var $scripts: NodeListOf<HTMLScriptElement> = $parentElement.querySelectorAll("script");
			for (var i = 0; i < $scripts.length; i++)
			{
				var $orgScript = $scripts.item(i);
				var $newScript = document.createElement("script");
				this.copyAttributesToElement($orgScript, $newScript);
				$newScript.innerHTML = $orgScript.innerHTML;
				var parent = $orgScript.parentNode;
				parent.insertBefore($newScript, $orgScript);
				parent.removeChild($orgScript);
			}
		}
		static destroyApp(newHtml: string)
		{
			/* example
				<!DOCTYPE html>
			<html>
			<head>
				<title>lol</title>
			</head>
			<body>
				<p>hmm</p>
			</body>
			</html>
				*/

			document.querySelector("ff-app").innerHTML = "";

			var parser = new DOMParser()
			var doc = parser.parseFromString(newHtml, "text/html");

			var $newHtml: HTMLHtmlElement = doc.querySelector("html");
			var $newHead = doc.head;
			var $newBody = doc.body;

			var $html = document.querySelector("html");
			var $head = document.head;
			var $body = document.body;

			this.removeAllAttributes($html);
			this.removeAllAttributes($head);
			this.removeAllAttributes($body);

			this.copyAttributesToElement($newHtml, $html);
			this.copyAttributesToElement($newHead, $head);
			this.copyAttributesToElement($newBody, $body);

			$head.innerHTML = "";
			$body.innerHTML = "";

			setTimeout(() =>
			{
				$head.innerHTML = $newHead.innerHTML;
				$body.innerHTML = $newBody.innerHTML;

				this.replaceDeadScriptsWithWorkingScripts(document.querySelector("html"));
			}, 0);

			window.onpopstate = () =>
			{
				location.href = location.href;
			};
		}
	}
	export namespace Utility
	{
		export interface UrlItems
		{
			path: string;
			query: any; //key/value
			hash: string;

		}
	}
	export class Helper
	{
		static textTrimStart(text: string, charToTrim: string)
		{
			while (true)
			{
				if (text.substr(0, 1) == charToTrim[0])
				{
					text = text.substr(1);
					continue;
				}
				break;
			}

			return text;
		}
		static textTrimEnd(text: string, charToTrim: string)
		{
			while (true)
			{
				if (text.substr(text.length - 1, 1) == charToTrim[0])
				{
					text = text.substr(0, text.length - 1);
					continue;
				}
				break;
			}

			return text;
		}
		static textReplaceAll(text: string, search: string, replacement: string)
		{
			return text.split(search).join(replacement);
		}
	}
	export class EventHandler
	{
		private events: any; //<key, function>
		constructor()
		{
			this.events = {};
		}
		subscribe(key: string, func: (arg?: any) => void): void
		{
			if (this.events[key] != null)
				console.error("Event with key '" + key + "' already exist");
			this.events[key] = func;
		}
		unsubscribe(key: string)
		{
			if (this.events[key] == null)
				console.error("Event with key '" + key + "' doesn't exist");
			delete this.events[key];
		}
		fireEvent(arg?: any)
		{
			for (var key in this.events)
				this.events[key](arg);
		}
	}
	export namespace Events
	{
		export interface IPrivateEventHandler
		{
			eventhandler: Engine.EventHandler;
		}
		export class OnChangePageBegun
		{
			private eventhandler: Engine.EventHandler = new Engine.EventHandler();
			subscribe(id: string, callback: (newUrl: string) => void)
			{
				this.eventhandler.subscribe(id, callback);
			}
			unsubscribe(id: string)
			{
				this.eventhandler.unsubscribe(id);
			}
		}
		export class OnChangePageDone
		{
			private eventhandler: Engine.EventHandler = new Engine.EventHandler();
			subscribe(id: string, callback: (newHtml: HTMLElement) => void)
			{
				this.eventhandler.subscribe(id, callback);
			}
			unsubscribe(id: string)
			{
				this.eventhandler.unsubscribe(id);
			}
		}
		export class OnAnimatingDone
		{
			private eventhandler: Engine.EventHandler = new Engine.EventHandler();
			subscribe(id: string, callback: () => void)
			{
				this.eventhandler.subscribe(id, callback);
			}
			unsubscribe(id: string)
			{
				this.eventhandler.unsubscribe(id);
			}
		}
		export class OnInitObjects
		{
			private eventhandler: Engine.EventHandler = new Engine.EventHandler();
			subscribe(id: string, callback: ($parentObj: HTMLElement) => void)
			{
				this.eventhandler.subscribe(id, callback);
			}
			unsubscribe(id: string)
			{
				this.eventhandler.unsubscribe(id);
			}
		}
	}

	export class EventsClass
	{
		/**
		Fired when a page is changing.
		Not fired when a user clicks on a hash link.
		In the callback, you can set the rc.historyData for the current page, before its changing to the new page.
			*/
		onChangePageBegun: Engine.Events.OnChangePageBegun;
		/**
		Fired when a page have been finished changing.
		In the callback, you can eg. use window.location.href to see the new url you are on.
			*/
		onChangePageDone: Engine.Events.OnChangePageDone;
		onAnimatingDone: Engine.Events.OnAnimatingDone;
		onInitObjects: Engine.Events.OnInitObjects;

		constructor()
		{
			this.onChangePageBegun = new Engine.Events.OnChangePageBegun();
			this.onChangePageDone = new Engine.Events.OnChangePageDone();
			this.onAnimatingDone = new Engine.Events.OnAnimatingDone();
			this.onInitObjects = new Engine.Events.OnInitObjects();
		}
	}
	/**
		Formula configurations that you can set or update
		*/
	export class Config
	{
		/**
		Gets/sets the scrolling extra space when clicking on a link with a hashtag
		If you put in 20, and you click on a link eg /hello/#foo, then it 
		will scroll to the element with id "foo", and 20 pixels above it.
			*/
		get hashScrollExtraSpace(): number
		{
			return coreMGR.miscMGR.hashScrollExtraSpace;
		}
		/**
		Gets/sets the scrolling offset when clicking on a link with a hashtag
		If you put in 20, and you click on a link eg /hello/#foo, then it 
		will scroll to the element with id "foo", and 20 pixels above it.
			*/
		set hashScrollExtraSpace(value: number)
		{
			coreMGR.miscMGR.hashScrollExtraSpace = value;
		}
		/**
		Set your own progressBar. If its not set, no progress bar will be shown 
		on the UI. The interface has a start() and done() function.
	
		It will fire start() when clicking an internal link where the webapp
		will dynamically change to the next page.
		It will fire done, when the page has changed finished.
	
		It should be set in the beginning of the App, probably through a 
		webobject in thAwake() or thStart() under the <ff-app> viewcontroller which will never
		be destroyed.
	
		An example of a ProgressBar library could be NProgress.
	
		Then you can put in the following value: 
		{ start: NProgress.start, done: NProgress.done }
	
		You can also try to quickly test it with console.log by putting this value in:
		{
			start: () => { console.log("start loading"); },
			done: () => { console.log("done loading"); }
		}
			*/
		setProgressBar(progressBar: Items.IProgressBar)
		{
			coreMGR.historyMGR.progressBar = progressBar;
		}
		/**
			* Set how long time the page will animate when changing the page (fading in and out)
			* Default is 100
			* If you call this with eg 200 ms, then remember to 
			* edit the Formula.scss $animateMs variable to 200 too.
			* Note, while the framework is animating the page, then it cant
			* change page to a new page
			*/
		setChangePageAnimMs(ms: number)
		{
			coreMGR.historyMGR.animateMs = ms;
		}
		setChangePageShallAnimate(shallAnimate: boolean)
		{
			coreMGR.historyMGR.shallAnimate = shallAnimate;
		}
	}
}

/**
Base class of webobjects
	*/
export abstract class WebObject
{
	$element: HTMLElement;
	id: string;
	get name(): string
	{
		return this.$element.getAttribute("ff-webobject");
	}
	private _isAlive: boolean = true;
	get isAlive(): boolean
	{
		return this._isAlive;
	}
}
export abstract class ViewController
{
	$element: HTMLElement;
	id: string;
	private _isAlive: boolean = true;
	get isAlive(): boolean
	{
		return this._isAlive;
	}
}
export abstract class BaseApp extends ViewController { }
export abstract class BasePage extends ViewController
{
	get name(): string
	{
		return this.$element.getAttribute("ff-name");
	}
}
export abstract class BaseLayout extends ViewController
{
	get name(): string
	{
		return this.$element.getAttribute("ff-name");
	}
}
/**
A namespace with interfaces
	*/
export namespace Items
{
	export interface ScriptItem
	{
		path: string;
		isModule: boolean;
	}
	/**
	The RData which contains many of the variables sent back from the server
	*/
	export interface RData
	{
		ff_appversion: number;
		ff_scripts: ScriptItem[];
		ff_targetfoldertype: string;
		ff_targetfolderpagename: string;
		ff_title: string;
		ff_webobjectstyles: string;
		ff_redirect: string;
		ff_scrollyextraspace: number;
		ff_polyfills: Polyfills;
		ff_cdn: string;
	}
	export interface IProgressBar
	{
		start(): void;
		done(): void;
	}
	export interface Polyfills
	{
		modernizr: { folderName: string, jsFiles: string[] }[],
		evaluation: { folderName: string, jsFiles: string[] }[]
	}
}

/**
	Events you can subscribe to
*/
export var events: Engine.EventsClass = new Engine.EventsClass();
export var config: Engine.Config = new Engine.Config();
/**
This is an instance of a class that is used by the framework.
Developer shouldn't be using this.
*/
export var coreMGR: Engine.CoreManager;
/**
The RData which contains many of the variables sent back from the server
*/
export function getRData(): Items.RData
{
	return this.coreMGR.rData;
}
export function start()
{
	//Gets called from App.cshtml
	coreMGR = new Engine.CoreManager();
	coreMGR.start();
}
/**
Gets all webobjects if id is not specified.
Gets a single webobject if id is specified
	*/
export function getWebObjects(id?: string | number)
{
	if (id == null)
		return coreMGR.webobjectMGR.webobjects_id;
	if (typeof id == "string")
	{
		if (isNaN(parseInt(id)))
			return coreMGR.webobjectMGR.webobjects_type[id];
		else
			return coreMGR.webobjectMGR.webobjects_id[id];
	}
	else
		return coreMGR.webobjectMGR.webobjects_id[id.toString()];
}
/**
	Changes the page. Firing this function will have the same effect,
	like clicking on a a-tag link.
	*/
export function changePage(href: string)
{
	coreMGR.historyMGR.linkClick(href);
}
/**
Gets/sets custom history data that you want. So when you click backward/forward, then
you can retrieve the same data you had.
	*/
export function historyData(data: any): any
{
	if (data == null)
		return coreMGR.historyMGR.getHistoryCustomData();
	else
		coreMGR.historyMGR.setHistoryCustomData(data);
}
export function createUId(): string
{
	return coreMGR.miscMGR.createUId();
}
export function initObjects($parentObj: HTMLElement)
{
	Engine.Utility.preProcessHtml($parentObj);
	coreMGR.webobjectMGR.createWebObjects($parentObj);
	coreMGR.webobjectMGR.startUnstartedWebObjects();
	//TODO: make a postProcessHtml
}